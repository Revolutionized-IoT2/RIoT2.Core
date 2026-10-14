using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;

namespace RIoT2.Core.Devices.Testing;

/// <summary>
/// Hosts a device plugin the way the node does: the node's services are registered, the plugin's
/// <see cref="IDevicePlugin.Initialize"/> runs, and devices are resolved from DI. Reports the devices raise are
/// captured in <see cref="Reports"/>. Lifecycle, command and refresh calls follow the node's dispatch rules
/// (the async interface when a device has one).
/// </summary>
public sealed class PluginTestHost : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly List<CapturedReport> _reports = [];
    private readonly Lock _reportsLock = new();
    private IReadOnlyList<IDevice> _devices;

    /// <param name="pluginType">The plugin class. It needs a public parameterless constructor, as in the node.</param>
    /// <param name="configureServices">Runs after the plugin registered its services, so a test can replace them, for example with hardware fakes.</param>
    public PluginTestHost(Type pluginType, Action<IServiceCollection> configureServices = null)
        : this(CreatePlugin(pluginType), configureServices) { }

    /// <inheritdoc cref="PluginTestHost(Type, Action{IServiceCollection})"/>
    public PluginTestHost(IDevicePlugin plugin, Action<IServiceCollection> configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ServiceCollection = new ServiceCollection();
        ServiceCollection.AddLogging();
        ServiceCollection.AddSingleton<ILogger>(NullLogger.Instance);
        ServiceCollection.AddSingleton<INodeConfigurationService>(NodeConfiguration);
        plugin.Initialize(ServiceCollection);
        configureServices?.Invoke(ServiceCollection);
        _provider = ServiceCollection.BuildServiceProvider();
    }

    /// <summary>The node configuration service the plugin's services receive.</summary>
    public TestNodeConfigurationService NodeConfiguration { get; } = new();

    /// <summary>The registrations after the plugin and the test configured them.</summary>
    public IServiceCollection ServiceCollection { get; }

    public IServiceProvider Services => _provider;

    /// <summary>Every device the plugin registered, resolved once, as the node does at startup.</summary>
    public IReadOnlyList<IDevice> Devices
    {
        get
        {
            if (_devices == null)
            {
                var devices = _provider.GetServices<IDevice>().ToList();
                foreach (var device in devices)
                    device.ReportUpdated += OnReportUpdated;
                _devices = devices;
            }
            return _devices;
        }
    }

    /// <summary>The single device of the given type.</summary>
    public TDevice GetDevice<TDevice>() where TDevice : IDevice => Devices.OfType<TDevice>().Single();

    /// <summary>Reports raised so far, in order.</summary>
    public IReadOnlyList<CapturedReport> Reports
    {
        get { lock (_reportsLock) return [.. _reports]; }
    }

    public void ClearReports()
    {
        lock (_reportsLock) _reports.Clear();
    }

    public static Task InitializeAsync(IDevice device, DeviceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(device);
        device.Initialize(configuration);
        return Task.CompletedTask;
    }

    public static Task StartAsync(IDevice device, CancellationToken cancellationToken = default) =>
        device is IAsyncDevice asynchronous
            ? asynchronous.StartAsync(cancellationToken)
            : Task.Run(device.Start, cancellationToken);

    public static Task StopAsync(IDevice device, CancellationToken cancellationToken = default) =>
        device is IAsyncDevice asynchronous
            ? asynchronous.StopAsync(cancellationToken)
            : Task.Run(device.Stop, cancellationToken);

    /// <summary>Sends a command the way the node's command service does. <paramref name="value"/> is the command value as JSON.</summary>
    public static Task CommandAsync(IDevice device, string commandId, string value, CancellationToken cancellationToken = default) => device switch
    {
        IAsyncCommandDevice asynchronous => asynchronous.ExecuteCommandAsync(commandId, value, cancellationToken),
        ICommandDevice command => Task.Run(() => command.ExecuteCommand(commandId, value), cancellationToken),
        _ => throw new InvalidOperationException($"{device?.GetType().FullName} doesn't accept commands.")
    };

    /// <summary>
    /// Refreshes one report, or the whole device when <paramref name="reportId"/> is null, as the node's scheduler does.
    /// </summary>
    public static Task RefreshAsync(IDevice device, string reportId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        var name = reportId ?? device.Id;
        return device switch
        {
            IAsyncRefreshableReportDevice asynchronous => asynchronous.RefreshReportAsync(device.Id, name, cancellationToken),
            IRefreshableReportDevice refreshable => Task.Run(() => refreshable.RefreshReport(device.Id, name), cancellationToken),
            _ => throw new InvalidOperationException($"{device.GetType().FullName} has no refreshable reports.")
        };
    }

    private void OnReportUpdated(IDevice sender, IReport report)
    {
        lock (_reportsLock) _reports.Add(new CapturedReport(sender, report));
    }

    internal static IDevicePlugin CreatePlugin(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        if (!typeof(IDevicePlugin).IsAssignableFrom(pluginType) || pluginType.IsAbstract)
            throw new ArgumentException($"{pluginType.FullName} isn't a concrete IDevicePlugin.", nameof(pluginType));
        return (IDevicePlugin)Activator.CreateInstance(pluginType);
    }

    public void Dispose()
    {
        if (_devices != null)
        {
            foreach (var device in _devices)
                device.ReportUpdated -= OnReportUpdated;
        }
        // Plugin services may implement only IAsyncDisposable, which the sync Dispose rejects.
        _provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_devices != null)
        {
            foreach (var device in _devices)
                device.ReportUpdated -= OnReportUpdated;
        }
        await _provider.DisposeAsync().ConfigureAwait(false);
    }
}
