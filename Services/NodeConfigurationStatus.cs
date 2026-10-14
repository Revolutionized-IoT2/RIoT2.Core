using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// A node's view of its configuration (design 7.2), which <see cref="NodeMqttService"/> publishes
    /// retained on <c>riot2/node/{id}/status</c> whenever it changes and on every connect. Until the
    /// first configuration is applied the state is <see cref="NodeConfigurationState.Applying"/>
    /// without an applied revision. Thread-safe.
    /// </summary>
    public sealed class NodeConfigurationStatus
    {
        private readonly object _gate = new object();
        private readonly Func<DateTime> _utcNow;
        private NodeStatusMessage _current;

        public NodeConfigurationStatus(Func<DateTime> utcNow = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _current = new NodeStatusMessage { State = NodeConfigurationState.Applying, TimeStamp = Now() };
        }

        /// <summary>Raised with a snapshot after every change and on <see cref="Republish"/>. Handlers must not block.</summary>
        public event Action<NodeStatusMessage> Changed;

        /// <summary>A snapshot of the current status.</summary>
        public NodeStatusMessage Current
        {
            get { lock (_gate) return Copy(_current); }
        }

        /// <summary>The hash of the last configuration applied, or <c>null</c>.</summary>
        public string AppliedHash
        {
            get { lock (_gate) return _current.AppliedHash; }
        }

        /// <summary>
        /// <c>true</c> when the configuration with <paramref name="hash"/> is applied and every device
        /// in it started, so receiving it again needs no work.
        /// </summary>
        public bool IsApplied(string hash)
        {
            lock (_gate)
                return hash != null && _current.AppliedHash == hash && _current.State == NodeConfigurationState.Applied
                    && (_current.FailedDevices == null || _current.FailedDevices.Count == 0);
        }

        /// <summary>A new configuration is being applied; the applied revision and hash stay until it is.</summary>
        public void Applying() => Update(status =>
        {
            status.State = NodeConfigurationState.Applying;
            status.Error = null;
        });

        /// <summary>
        /// The configuration with <paramref name="revision"/> and <paramref name="hash"/> (both <c>null</c>
        /// from an orchestrator before 1.5.0) is applied. Devices that didn't start are listed, but the
        /// configuration itself counts as applied (design 7.2, decision 5).
        /// </summary>
        public void Applied(int? revision, string hash, IEnumerable<FailedDevice> failedDevices = null) => Update(status =>
        {
            var failed = failedDevices?.Where(d => d != null).ToList();
            status.State = NodeConfigurationState.Applied;
            status.AppliedRevision = revision;
            status.AppliedHash = hash;
            status.Error = null;
            status.FailedDevices = failed == null || failed.Count == 0 ? null : failed;
        });

        /// <summary>The configuration could not be fetched or applied; the node keeps running the previous one.</summary>
        public void Failed(string error) => Update(status =>
        {
            status.State = NodeConfigurationState.Failed;
            status.Error = string.IsNullOrEmpty(error) ? "The configuration could not be applied." : error;
        });

        /// <summary>A new plugin package is installed when the node restarts.</summary>
        public void PendingRestart() => Update(status =>
        {
            status.State = NodeConfigurationState.PendingRestart;
            status.Error = null;
        });

        /// <summary>The installed plugin package, reported with the status.</summary>
        public void SetPluginManifest(PackageManifest manifest) => Update(status => status.PluginManifest = manifest);

        /// <summary>Raises <see cref="Changed"/> with the unchanged status, for example to answer a configuration command.</summary>
        public void Republish() => Update(_ => { });

        private void Update(Action<NodeStatusMessage> change)
        {
            NodeStatusMessage snapshot;
            lock (_gate)
            {
                change(_current);
                _current.TimeStamp = Now();
                snapshot = Copy(_current);
            }
            Changed?.Invoke(snapshot);
        }

        private long Now() => _utcNow().ToEpoch();

        private static NodeStatusMessage Copy(NodeStatusMessage status) => new NodeStatusMessage
        {
            AppliedRevision = status.AppliedRevision,
            AppliedHash = status.AppliedHash,
            State = status.State,
            Error = status.Error,
            FailedDevices = status.FailedDevices?.Select(d => new FailedDevice { Id = d.Id, Message = d.Message }).ToList(),
            PluginManifest = status.PluginManifest,
            TimeStamp = status.TimeStamp,
            ContractVersion = status.ContractVersion
        };
    }
}
