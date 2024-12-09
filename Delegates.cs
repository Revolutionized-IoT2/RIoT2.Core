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
    public delegate void StoredObjectEventHandler(Type type, dynamic obj, OperationType changeType);
}
