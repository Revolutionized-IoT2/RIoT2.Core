using RIoT2.Core.Abstracts;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

namespace RIoT2.Core.Diagnostics
{
    /// <summary>
    /// The node's instruments (meter <see cref="MeterNames.Node"/>, design 7.4 phase 4), recorded by
    /// <see cref="Services.NodeMqttService"/>, <see cref="DeviceServiceBase"/> and
    /// <see cref="Services.NodeConfigurationStatus"/>. Nothing is measured while no listener is attached.
    /// </summary>
    public static class NodeMetrics
    {
        public const string DevicesName = "riot2.node.devices";
        public const string DeviceRestartsName = "riot2.node.device_restarts";
        public const string CommandsName = "riot2.node.commands";
        public const string ReportsPublishedName = "riot2.node.reports.published";
        public const string ReportsDroppedName = "riot2.node.reports.dropped";
        public const string ConfigAppliesName = "riot2.node.config_applies";

        /// <summary><see cref="ConfigApplies"/> results.</summary>
        public const string Applied = "applied";
        public const string Failed = "failed";
        public const string Unchanged = "unchanged";

        private static readonly Meter Meter = new Meter(MeterNames.Node);
        private static readonly List<WeakReference<DeviceServiceBase>> Services = new List<WeakReference<DeviceServiceBase>>();

        /// <summary>
        /// Running devices stopped and started again because their configuration changed or was removed.
        /// It stays at zero across a reconnect or an unchanged configuration (backlog item 18).
        /// </summary>
        public static readonly Counter<long> DeviceRestarts = Meter.CreateCounter<long>(DeviceRestartsName, "{restart}",
            "Running devices stopped and started again because their configuration changed or was removed.");

        /// <summary>Commands handled, by <c>outcome</c>: the command result status (<c>executed</c>, <c>failed</c>, …).</summary>
        public static readonly Counter<long> Commands = Meter.CreateCounter<long>(CommandsName, "{command}",
            "Commands handled by the node, by outcome.");

        public static readonly Counter<long> ReportsPublished = Meter.CreateCounter<long>(ReportsPublishedName, "{report}",
            "Reports published to the broker.");

        public static readonly Counter<long> ReportsDropped = Meter.CreateCounter<long>(ReportsDroppedName, "{report}",
            "Reports dropped because the publish queue was full.");

        /// <summary>Configuration commands handled, by <c>result</c>: <c>applied</c>, <c>failed</c> or <c>unchanged</c>.</summary>
        public static readonly Counter<long> ConfigApplies = Meter.CreateCounter<long>(ConfigAppliesName, "{configuration}",
            "Configuration commands handled, by result: applied, failed or unchanged (nothing to do).");

        static NodeMetrics()
        {
            Meter.CreateObservableGauge(DevicesName, ObserveDevices, "{device}", "Devices on the node, by state.");
        }

        /// <summary>Counts <paramref name="service"/>'s devices in <c>riot2.node.devices</c> while it is alive.</summary>
        public static void Track(DeviceServiceBase service)
        {
            if (service == null)
                return;
            lock (Services)
            {
                Services.RemoveAll(reference => !reference.TryGetTarget(out _));
                Services.Add(new WeakReference<DeviceServiceBase>(service));
            }
        }

        /// <summary>Stops counting <paramref name="service"/>'s devices.</summary>
        public static void Untrack(DeviceServiceBase service)
        {
            lock (Services)
                Services.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, service));
        }

        /// <summary>The <c>outcome</c> or <c>result</c> tag value for a status name.</summary>
        public static KeyValuePair<string, object?> Tag(string name, string value) => new KeyValuePair<string, object?>(name, value);

        private static IEnumerable<Measurement<int>> ObserveDevices()
        {
            List<DeviceServiceBase> services;
            lock (Services)
                services = Services.Select(reference => reference.TryGetTarget(out var target) ? target : null).OfType<DeviceServiceBase>().ToList();

            var counts = new Dictionary<DeviceState, int>();
            foreach (DeviceState state in Enum.GetValues(typeof(DeviceState)))
                counts[state] = 0;
            foreach (var service in services)
            {
                foreach (var device in service.Devices?.ToList() ?? new List<Interfaces.IDevice>())
                {
                    if (device != null)
                        counts[device.State] = counts.TryGetValue(device.State, out var count) ? count + 1 : 1;
                }
            }
            return counts.Select(entry => new Measurement<int>(entry.Value, Tag("state", StateName(entry.Key))));
        }

        // camelCase, as the state is written in JSON.
        private static string StateName(DeviceState state)
        {
            var name = state.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
