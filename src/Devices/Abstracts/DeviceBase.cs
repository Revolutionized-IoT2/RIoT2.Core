using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using RIoT2.Core.Plugins;
using RIoT2.Core.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace RIoT2.Core.Abstracts
{
    /// <summary>
    /// The legacy synchronous base class (plan M12 step 5): new devices derive from <see cref="AsyncDeviceBase"/>. The
    /// node runs this lifecycle on a thread-pool thread (<see cref="Plugins.DeviceCalls"/>). It stays for one major
    /// release, without <c>[Obsolete]</c> until every catalog device has moved.
    /// </summary>
    public abstract class DeviceBase
    {
        public DeviceBase(ILogger logger)
        {
            Logger = logger;
            ReportTemplates = new List<ReportTemplate>();
            _previousReports = new List<Report>();
            _numericTrends = new Dictionary<string, int>();
            State = DeviceState.Unknown;
            StateMessage = "";
        }

        public DeviceConfiguration Configuration { get { return _configuration; } }
        private List<Report> _previousReports;
        private Dictionary<string, int> _numericTrends;
        // Set by Initialize, before the device is configured or started.
        private DeviceConfiguration _configuration = null!;
        private readonly object _sendReportIfValueChangedLock = new object();
        public event ReportUpdatedHandler? ReportUpdated;

        public DeviceState State { get; private set; }
        public string StateMessage { get; private set; }
        protected void SetState(DeviceState state, string message = "")
        {
            State = state;
            StateMessage = message;
        }
        public string Id { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;

        //public int RefreshInterval { get; private set; }

        public ILogger Logger { get; private set; }

        public virtual IEnumerable<CommandTemplate>? CommandTemplates { get; private set; }

        public virtual IEnumerable<ReportTemplate> ReportTemplates { get; private set; }

        public abstract void ConfigureDevice();

        public abstract void StartDevice();

        public abstract void StopDevice();

        public void Start() 
        {
            try
            {
                State = DeviceState.Running;
                StateMessage = "";
                StartDevice();
            }
            catch (Exception x) 
            {
                State = DeviceState.Error;
                StateMessage = x.Message;
                throw new Exception($"Error starting {Name}:{Id}", x);
            }
        }

        public void Stop()
        {
            try
            {
                StopDevice();
                State = DeviceState.Stopped;
                StateMessage = "";
            }
            catch (Exception x)
            {
                State = DeviceState.Error;
                StateMessage = x.Message;
                throw new Exception($"Error stopping {Name}:{Id}", x);
            }
        }

        public void Initialize(DeviceConfiguration configuration)
        {
            _configuration = configuration;
            configureBase();
        }

        private void configureBase()
        {
            Id = _configuration.Id;
            Name = _configuration.Name;
            ReportTemplates = _configuration.ReportTemplates;
            CommandTemplates = _configuration.CommandTemplates;

            try
            {
                State = DeviceState.Initialized;
                StateMessage = "";
                BindOptions();
                ConfigureDevice();
            }
            catch (Exception x)
            {
                State = DeviceState.Error;
                StateMessage = x.Message;
                throw new Exception($"Error configuring {Name}:{Id}", x);
            }
        }

        // Lets RIoT2.Core.Devices.Testing record which parameter keys a device reads (PluginConformance).
        internal Action<string>? ParameterRead { get; set; }

        // DeviceBase<TOptions> binds its options here, before ConfigureDevice.
        internal virtual void BindOptions() { }

        /// <summary>
        /// Reads a device parameter. The key matches case-insensitively when no key matches exactly (since Core
        /// 1.16.0), because the configuration download camel-cases parameter keys (divergence C1).
        /// </summary>
        public T? GetConfiguration<T>(string parameter)
        {
            ParameterRead?.Invoke(parameter);
            var value = DeviceOptions.Find(_configuration.DeviceParameters, parameter);
            if (value == null)
                return default(T);

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                return (T?)converter.ConvertFromInvariantString(value);
            }
            catch (Exception x)
            {
                Logger.LogError(x, $"Could not convert parameter {parameter} to type {typeof(T).ToString()}");
            }
            return default(T);
        }

        public object? ConvertStringValueToExpectedValue(ValueType type, string value, object? model = null)
        {
            switch (type)
            {
                case ValueType.Number:
                    float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number);
                    return number;
                case ValueType.Boolean:
                    bool.TryParse(value, out var boolValue);
                    return boolValue;
                case ValueType.Text:
                    return value;
                case ValueType.Entity:
                    if (model == null)
                        return null;

                    return Json.Deserialize(value, model.GetType());
            }
            return null;
        }

        public ValueType GetObjectValueType(object o)
        {
            if (o is string)
                return ValueType.Text;

            if (o is bool)
                return ValueType.Boolean;

            if (o is float || o is int || o is decimal || o is double || o is short || o is byte || o is sbyte || o is ushort || o is uint || o is long || o is ulong)
                return ValueType.Number;

            return ValueType.Entity;
        }

        public void SendReport(IDevice device, Report report)
        {
            if(State != DeviceState.Running)
                Logger.LogWarning($"Could not send report. Device {Name} is not running");
            else 
                ReportUpdated?.Invoke(device, report);
        }

        /// <summary>
        /// This method sends report only if the value is different. If value is number, threshold can be used
        /// </summary>
        /// <param name="device"></param>
        /// <param name="report"></param>
        /// <param name="threshold"></param>
        public void SendReportIfValueChanged(IDevice device, Report report, double? threshold = null)
        {
            if (State != DeviceState.Running) 
            {
                Logger.LogWarning($"Could not send report. Device {Name} is not running");
                return;
            }

            lock (_sendReportIfValueChangedLock)
            {
                var prevReport = _previousReports.FirstOrDefault(x => x.Id == report.Id);

                if (prevReport == null)
                {
                    storePreviousReport(report);
                    ReportUpdated?.Invoke(device, report);
                    return;
                }

                if (report.Value != null && report.Value.Type == ValueType.Number)
                {
                    bool noOperation = true;
                    try
                    {
                        double prevValue = prevReport.Value.GetValue<double>();
                        double currentValue = report.Value.GetValue<double>();
                        noOperation = !thresholdOrTrendExeeded(report.Id, prevValue, currentValue, threshold);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error checking threshold for report {ReportId}", report.Id);
                    }

                    if (noOperation)
                        return;
                }
                else
                {
                    var prevReportValueAsJson = prevReport.Value?.ToJson();
                    var reportValueAsString = report.Value?.ToJson();
                    if (prevReportValueAsJson == reportValueAsString)
                        return;
                }

                //Update previous reports (store a snapshot so later caller mutations don't affect comparisons)
                _previousReports.Remove(prevReport);
                storePreviousReport(report);

                //and send report
                ReportUpdated?.Invoke(device, report);
            }
        }

        private void storePreviousReport(Report report)
        {
            _previousReports.Add(new Report
            {
                Id = report.Id,
                TimeStamp = report.TimeStamp,
                Filter = report.Filter,
                Value = report.Value?.Copy()!
            });
        }

        public void RefreshReport(string group, string name)
        {
            if (State != DeviceState.Running)
            {
                Logger.LogWarning($"Could not refresh report. Device {Name} is not running");
                return;
            }

            if (group != Id)
                return;

            try
            {
                Refresh(ReportTemplates.FirstOrDefault(x => x.Id == name));
            }
            catch (Exception x)
            {
                State = DeviceState.Error;
                StateMessage = x.Message;
                Logger.LogError($"Error in RefreshReport for device {Name}: {x.Message}");
            }
        }

        public virtual void Refresh(ReportTemplate? report) { }

        private bool thresholdOrTrendExeeded(string reportId, double prevValue, double newValue, double? threshold)
        {
            //No threshold configured -> report on any change.
            if (!threshold.HasValue)
                return prevValue != newValue;

            double effectiveThreshold = threshold.Value;
            if ((prevValue + effectiveThreshold) < newValue || (prevValue - effectiveThreshold) > newValue)
                return true;

            //Within threshold: track directional trend so slow drifts are eventually reported.
            if (!_numericTrends.ContainsKey(reportId))
            {
                _numericTrends.Add(reportId, 0);
                return false;
            }

            int currentTrendValue = _numericTrends[reportId];
            if (newValue > prevValue)
                currentTrendValue++;

            if (newValue < prevValue)
                currentTrendValue--;

            if (currentTrendValue >= 3 || currentTrendValue <= -3)
            {
                _numericTrends[reportId] = 0;
                return true;
            }

            _numericTrends[reportId] = currentTrendValue;
            return false;
        }
    }
}
