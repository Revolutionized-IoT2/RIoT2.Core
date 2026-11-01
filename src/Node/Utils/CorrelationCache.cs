using System;
using System.Collections.Generic;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Remembers recently seen correlation ids and how their commands ended, so a node runs a
    /// redelivered command only once and can answer the redelivery (design 7.1). Keeps at most
    /// <see cref="Capacity"/> ids, each for <see cref="Lifetime"/>; the oldest id is forgotten
    /// first. Thread-safe.
    /// </summary>
    public sealed class CorrelationCache
    {
        public const int DefaultCapacity = 256;
        public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

        private sealed class Entry
        {
            public string Id;
            public DateTime Seen;
            public string Status;
            public string Error;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, LinkedListNode<Entry>> _ids = new Dictionary<string, LinkedListNode<Entry>>(StringComparer.Ordinal);
        private readonly LinkedList<Entry> _order = new LinkedList<Entry>();
        private readonly Func<DateTime> _utcNow;

        public CorrelationCache(int capacity = DefaultCapacity, TimeSpan? lifetime = null, Func<DateTime> utcNow = null)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Lifetime = lifetime ?? DefaultLifetime;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public int Capacity { get; }
        public TimeSpan Lifetime { get; }

        /// <summary>
        /// Records <paramref name="id"/> as running and returns <c>true</c>, or returns <c>false</c> when
        /// it is already known; <see cref="TryGet"/> then tells whether it is still running.
        /// </summary>
        public bool TryAdd(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            lock (_gate)
            {
                Prune();
                if (_ids.ContainsKey(id))
                    return false;
                if (_ids.Count >= Capacity)
                    Remove(_order.First);
                _ids[id] = _order.AddLast(new Entry { Id = id, Seen = _utcNow() });
                return true;
            }
        }

        /// <summary>
        /// Looks up <paramref name="id"/>. <paramref name="status"/> is <c>null</c> while its command
        /// runs, and its final <see cref="Models.CommandResultStatus"/> afterwards.
        /// </summary>
        public bool TryGet(string id, out string status, out string error)
        {
            lock (_gate)
            {
                Prune();
                if (id != null && _ids.TryGetValue(id, out var node))
                {
                    status = node.Value.Status;
                    error = node.Value.Error;
                    return true;
                }
                status = error = null;
                return false;
            }
        }

        /// <summary>Records how the command ended. Nothing happens when the id was forgotten meanwhile.</summary>
        public void Complete(string id, string status, string error = null)
        {
            lock (_gate)
            {
                if (id != null && _ids.TryGetValue(id, out var node))
                {
                    node.Value.Status = status;
                    node.Value.Error = error;
                }
            }
        }

        /// <summary>Forgets <paramref name="id"/>, so its next delivery runs again.</summary>
        public void Remove(string id)
        {
            lock (_gate)
            {
                if (id != null && _ids.TryGetValue(id, out var node))
                    Remove(node);
            }
        }

        private void Prune()
        {
            var now = _utcNow();
            while (_order.First != null && now - _order.First.Value.Seen >= Lifetime)
                Remove(_order.First);
        }

        private void Remove(LinkedListNode<Entry> node)
        {
            _ids.Remove(node.Value.Id);
            _order.Remove(node);
        }
    }
}
