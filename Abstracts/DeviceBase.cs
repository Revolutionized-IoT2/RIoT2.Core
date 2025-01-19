using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RIoT2.Core.Abstracts
{
    public abstract class DeviceBase
    {
        public DeviceBase(ILogger logger)
        {
            Logger = logger;
            CommandTemplates = new List<CommandTemplate>();
            ReportTemplates = new List<ReportTemplate>();
            _previousReports = new List<Report>();
            _numericTrends = new Dictionary<string, int>();
            State = DeviceState.Unknown;
            StateMessage = "";
        }

        public DeviceConfiguration Configuration { get { return _configuration; } }
        private List<Report> _previousReports;
        private Dictionary<string, int> _numericTrends;
        private DeviceConfiguration _configuration;
        private readonly object _sendReportIfValueChangedLock = new object();
        public event ReportUpdatedHandler ReportUpdated;

        public DeviceState State { get; private set; }
        public string StateMessage { get; private set; }
        public string Id { get; private set; }
        public string Name { get; private set; }

        //public int RefreshInterval { get; private set; }

        public ILogger Logger { get; private set; }

        public virtual IEnumerable<CommandTemplate> CommandTemplates { get; private set; }

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
                ConfigureDevice();
            }
            catch (Exception x)
            {
                State = DeviceState.Error;
                StateMessage = x.Message;
            }
        }

        public T GetConfiguration<T>(string parameter)
        {
            if (_configuration.DeviceParameters == null || !_configuration.DeviceParameters.ContainsKey(parameter))
                return default(T);

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                return (T)(converter.ConvertFromInvariantString(_configuration.DeviceParameters[parameter]));
            }
            catch (Exception x)
            {
                Logger.LogError(x, $"Could not convert parameter {parameter} to type {typeof(T).ToString()}");
            }
            return default(T);
        }

        public object ConvertStringValueToExpectedValue(ValueType type, string value, object model = null)
        {
            switch (type)
            {
                case ValueType.Number:
                    float.TryParse(value, out var number);
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
                ReportUpdated(device, report);
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
                    _previousReports.Add(report);
                    ReportUpdated(device, report);
                    return;
                }

                ValueType reportType = GetObjectValueType(report.Value);
                if (reportType == ValueType.Number)
                {
                    bool noOperation = true;
                    try
                    {
                        double prevValue = Convert.ToDouble(prevReport.Value);
                        double currentValue = Convert.ToDouble(report.Value);
                        noOperation = !thresholdOrTrendExeeded(report.Id, prevValue, currentValue, threshold);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error checking threshold", report);
                    }

                    if (noOperation)
                        return;
                }
                else
                {
                    var prevReportValueAsJson = prevReport.Value.ToJson();
                    var reportValueAsString = report.Value.ToJson();
                    if (prevReportValueAsJson == reportValueAsString)
                        return;
                }

                //Update previous reports
                _previousReports.Remove(prevReport);
                _previousReports.Add(report);

                //and send report
                ReportUpdated(device, report);
            }
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
            }
        }

        public virtual void Refresh(ReportTemplate report) { }

        private bool thresholdOrTrendExeeded(string reportId, double prevValue, double newValue, double? threshold)
        {
            if ((prevValue + threshold) < newValue || (prevValue - threshold) > newValue)
                return true;

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

            if (currentTrendValue > 3 || currentTrendValue < 3)
            {
                _numericTrends[reportId] = 0;
                return true;
            }

            return false;
        }
    }
}
