using System.Globalization;
using Microsoft.Extensions.Logging;
using RIoT2.Core;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using RIoT2.Core.Plugins;
using ValueType = RIoT2.Core.ValueType;

namespace Contoso.Riot2Plugin.Devices
{
    /// <summary>
    /// The device's parameters. The node lists them in the device's template (<c>parameterDefinitions</c>), the UI
    /// edits them with typed inputs, and the base class binds them before <see cref="Counter.ConfigureDevice"/>.
    /// </summary>
    public sealed class CounterOptions
    {
        [DeviceParameter("step", Default = "1", Description = "How much the counter grows on each refresh")]
        public double Step { get; set; }

        [DeviceParameter("start", Default = "0", Description = "The value after a start")]
        public double Start { get; set; }
    }

    /// <summary>
    /// A sample device: it counts up on every scheduled refresh and reports the value; the command <c>set</c>
    /// replaces it. Replace it with your own: talk to the hardware or service in the async methods, and never block.
    /// </summary>
    public class Counter : AsyncDeviceBase<CounterOptions>, IAsyncCommandDevice, IDeviceWithConfiguration
    {
        public const string ValueAddress = "value";
        public const string SetAddress = "set";
        private double _value;

        public Counter(ILogger logger) : base(logger) { }

        public override void ConfigureDevice() { }

        protected override Task StartDeviceAsync(CancellationToken cancellationToken)
        {
            _value = Options.Start;
            return Task.CompletedTask;
        }

        protected override Task StopDeviceAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task RefreshAsync(ReportTemplate? report, CancellationToken cancellationToken)
        {
            _value += Options.Step;
            Report();
            return Task.CompletedTask;
        }

        /// <summary>The command <c>set</c>: <paramref name="value"/> is the command value as JSON (a number).</summary>
        public Task ExecuteCommandAsync(string commandId, string value, CancellationToken cancellationToken)
        {
            var command = CommandTemplates?.FirstOrDefault(c => c.Id == commandId);
            if (command?.Address != SetAddress)
                throw new ArgumentException($"Unknown command {commandId}.", nameof(commandId));
            _value = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            Report();
            return Task.CompletedTask;
        }

        private void Report()
        {
            var report = ReportTemplates.FirstOrDefault(r => r.Address == ValueAddress);
            if (report != null)
                SendReport(this, new Report { Id = report.Id, TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Value = new ValueModel(_value) });
        }

        /// <summary>What the UI offers when the device is added; the node adds the parameters from <see cref="CounterOptions"/>.</summary>
        public DeviceConfiguration GetConfigurationTemplate() => new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Counter",
            ClassFullName = GetType().FullName!,
            RefreshSchedule = DeviceTemplates.DefaultRefreshSchedule,
            ReportTemplates =
            [
                new ReportTemplate { Id = Guid.NewGuid().ToString(), Name = "Counter value", Address = ValueAddress, Type = ValueType.Number, Model = 0 }
            ],
            CommandTemplates =
            [
                new CommandTemplate { Id = Guid.NewGuid().ToString(), Name = "Set counter", Address = SetAddress, Type = ValueType.Number, Model = 0 }
            ]
        };
    }
}
