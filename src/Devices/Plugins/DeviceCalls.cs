using System;
using System.Threading;
using System.Threading.Tasks;
using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// How a host calls a device (plan M12 step 5): through the async interfaces only. A legacy synchronous device
    /// (<see cref="Abstracts.DeviceBase"/>, or a plugin built before Core 1.11) is adapted here, on a thread-pool
    /// thread, so the host has one code path and never blocks. The node, <c>PluginTestHost</c> and
    /// <c>PluginConformance</c> all call devices through this class.
    /// </summary>
    public static class DeviceCalls
    {
        /// <summary>Whether the device has the awaited lifecycle (<see cref="IAsyncDevice"/>); otherwise it is adapted.</summary>
        public static bool IsAsynchronous(IDevice device) => device is IAsyncDevice;

        public static Task StartAsync(IDevice device, CancellationToken cancellationToken)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            return device is IAsyncDevice asynchronous
                ? asynchronous.StartAsync(cancellationToken)
                : Task.Run(device.Start, cancellationToken);
        }

        public static Task StopAsync(IDevice device, CancellationToken cancellationToken)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            return device is IAsyncDevice asynchronous
                ? asynchronous.StopAsync(cancellationToken)
                : Task.Run(device.Stop, cancellationToken);
        }

        /// <summary>Runs a command; <paramref name="value"/> is the command value as JSON.</summary>
        public static Task ExecuteCommandAsync(IDevice device, string commandId, string value, CancellationToken cancellationToken)
        {
            switch (device)
            {
                case IAsyncCommandDevice asynchronous:
                    return asynchronous.ExecuteCommandAsync(commandId, value, cancellationToken);
                case ICommandDevice command:
                    return Task.Run(() => command.ExecuteCommand(commandId, value), cancellationToken);
                case null:
                    throw new ArgumentNullException(nameof(device));
                default:
                    throw new InvalidOperationException($"{device.GetType().FullName} doesn't accept commands.");
            }
        }

        /// <summary>Refreshes report <paramref name="name"/> of device <paramref name="group"/> (the device's id for all of them).</summary>
        public static Task RefreshAsync(IDevice device, string group, string name, CancellationToken cancellationToken)
        {
            switch (device)
            {
                case IAsyncRefreshableReportDevice asynchronous:
                    return asynchronous.RefreshReportAsync(group, name, cancellationToken);
                case IRefreshableReportDevice refreshable:
                    return Task.Run(() => refreshable.RefreshReport(group, name), cancellationToken);
                case null:
                    throw new ArgumentNullException(nameof(device));
                default:
                    throw new InvalidOperationException($"{device.GetType().FullName} has no refreshable reports.");
            }
        }
    }
}
