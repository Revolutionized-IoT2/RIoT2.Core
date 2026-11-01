using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;

namespace RIoT2.Core
{
    public delegate void DeviceServiceUpdatedHandler(ServiceEvent serviceEvent);
    public delegate void DeviceConfigurationUpdatedHandler();
    public delegate void ReportUpdatedHandler(IDevice sender, IReport report);
    public delegate void MqttMessageReceivedHandler(MqttEventArgs mqttEventArgs);
    public delegate void SchedulerEventHandler(string group, string name);
    public delegate void WebMessageHandler(string method, string body, Dictionary<string, string> querystrings, Dictionary<string, string> headers);
    [Obsolete("Orchestrator-only; the orchestrator has its own typed store since 1.11.0 (I26). Removed with Newtonsoft (I45).")]
    public delegate void StoredObjectEventHandler(Type type, dynamic obj, OperationType changeType);
}
