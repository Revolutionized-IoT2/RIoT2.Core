using RIoT2.Core.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Source-generated metadata for the wire contract types (ADR 0015, plan M2 step 2, Core 2.0.0). It is the
    /// first resolver of <see cref="RIoT2JsonOptions"/>, so these types don't need reflection to build their
    /// contract; every other type falls back to reflection. The options' converters and member order still apply,
    /// so the output is the same, byte for byte (the golden files in <c>contracts/golden</c>).
    /// </summary>
    /// <remarks>Add a type here when it joins the contract (<see cref="Schema.ContractSchemas"/>).</remarks>
    [JsonSourceGenerationOptions(IncludeFields = true)]
    [JsonSerializable(typeof(Report))]
    [JsonSerializable(typeof(Command))]
    [JsonSerializable(typeof(CommandResult))]
    [JsonSerializable(typeof(ConfigurationCommand))]
    [JsonSerializable(typeof(NodeOnlineMessage))]
    [JsonSerializable(typeof(NodeStatusMessage))]
    [JsonSerializable(typeof(NodeDeviceConfiguration))]
    [JsonSerializable(typeof(List<NodeDeviceConfiguration>))]
    [JsonSerializable(typeof(DeviceConfiguration))]
    [JsonSerializable(typeof(List<DeviceConfiguration>))]
    [JsonSerializable(typeof(NodeReportTemplate))]
    [JsonSerializable(typeof(List<NodeReportTemplate>))]
    [JsonSerializable(typeof(NodeCommandTemplate))]
    [JsonSerializable(typeof(List<NodeCommandTemplate>))]
    [JsonSerializable(typeof(ReportTemplate))]
    [JsonSerializable(typeof(CommandTemplate))]
    [JsonSerializable(typeof(Variable))]
    [JsonSerializable(typeof(List<Variable>))]
    [JsonSerializable(typeof(ValueSchema))]
    [JsonSerializable(typeof(DashboardConfiguration))]
    [JsonSerializable(typeof(DeviceStatus))]
    [JsonSerializable(typeof(List<DeviceStatus>))]
    [JsonSerializable(typeof(PackageManifest))]
    [JsonSerializable(typeof(SecurityInfo))]
    [JsonSerializable(typeof(ForecastSeries))]
    [JsonSerializable(typeof(ForecastSummary))]
    public partial class RIoT2JsonContext : JsonSerializerContext
    {
    }
}
