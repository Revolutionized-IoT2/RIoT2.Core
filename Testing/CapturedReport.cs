using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Devices.Testing;

/// <summary>A report a device raised while it was hosted by <see cref="PluginTestHost"/>.</summary>
public sealed class CapturedReport(IDevice device, IReport report)
{
    public IDevice Device { get; } = device;
    public IReport Report { get; } = report;
}
