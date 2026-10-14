using RIoT2.Core.Models;
using ValueType = RIoT2.Core.ValueType;

namespace RIoT2.Core.Devices.Testing;

/// <summary>Builds a <see cref="DeviceConfiguration"/> the way the orchestrator sends one to a node.</summary>
public sealed class DeviceConfigurationBuilder
{
    private readonly DeviceConfiguration _configuration;

    private DeviceConfigurationBuilder(DeviceConfiguration configuration) => _configuration = configuration;

    /// <summary>An empty configuration for the device type, with a new id and no parameters or templates.</summary>
    public static DeviceConfigurationBuilder For<TDevice>() => For(typeof(TDevice));

    /// <inheritdoc cref="For{TDevice}"/>
    public static DeviceConfigurationBuilder For(Type deviceType)
    {
        ArgumentNullException.ThrowIfNull(deviceType);
        return new DeviceConfigurationBuilder(new DeviceConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = deviceType.Name,
            ClassFullName = deviceType.FullName,
            DeviceParameters = [],
            ReportTemplates = [],
            CommandTemplates = []
        });
    }

    /// <summary>A copy of a device's configuration template, as a user would save it unchanged.</summary>
    public static DeviceConfigurationBuilder FromTemplate(DeviceConfiguration template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new DeviceConfigurationBuilder(new DeviceConfiguration
        {
            Id = string.IsNullOrEmpty(template.Id) ? Guid.NewGuid().ToString() : template.Id,
            Name = template.Name,
            ClassFullName = template.ClassFullName,
            RefreshSchedule = template.RefreshSchedule,
            DeviceParameters = template.DeviceParameters == null ? [] : new Dictionary<string, string>(template.DeviceParameters),
            ReportTemplates = template.ReportTemplates == null ? [] : [.. template.ReportTemplates],
            CommandTemplates = template.CommandTemplates == null ? null : [.. template.CommandTemplates],
            MatterEndpoints = template.MatterEndpoints == null ? null : [.. template.MatterEndpoints]
        });
    }

    public DeviceConfigurationBuilder WithId(string id)
    {
        _configuration.Id = id;
        return this;
    }

    public DeviceConfigurationBuilder WithName(string name)
    {
        _configuration.Name = name;
        return this;
    }

    public DeviceConfigurationBuilder WithParameter(string key, string value)
    {
        _configuration.DeviceParameters ??= [];
        _configuration.DeviceParameters[key] = value;
        return this;
    }

    public DeviceConfigurationBuilder WithoutParameter(string key)
    {
        _configuration.DeviceParameters?.Remove(key);
        return this;
    }

    public DeviceConfigurationBuilder WithRefreshSchedule(string cron)
    {
        _configuration.RefreshSchedule = cron;
        return this;
    }

    /// <summary>Adds a report template. The id defaults to the address.</summary>
    public DeviceConfigurationBuilder WithReport(string address, ValueType type = ValueType.Text, string id = null, string name = null)
    {
        _configuration.ReportTemplates ??= [];
        _configuration.ReportTemplates.Add(new ReportTemplate { Id = id ?? address, Address = address, Name = name ?? address, Type = type });
        return this;
    }

    /// <summary>Adds a command template. The id defaults to the address.</summary>
    public DeviceConfigurationBuilder WithCommand(string address, ValueType type = ValueType.Text, string id = null, string name = null)
    {
        _configuration.CommandTemplates ??= [];
        _configuration.CommandTemplates.Add(new CommandTemplate { Id = id ?? address, Address = address, Name = name ?? address, Type = type });
        return this;
    }

    public DeviceConfiguration Build() => _configuration;
}
