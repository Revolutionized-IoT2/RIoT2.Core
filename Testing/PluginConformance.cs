using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using RIoT2.Core.Plugins;

namespace RIoT2.Core.Devices.Testing;

/// <summary>
/// The conformance test every device plugin runs. It loads the plugin as the node does and checks each device:
/// <list type="bullet">
/// <item>the assembly has exactly one <see cref="IDevicePlugin"/> type with a public parameterless constructor;</item>
/// <item>every registered device resolves, and no device type is registered twice;</item>
/// <item>the device returns a configuration template (the node's default when it doesn't implement
/// <see cref="IDeviceWithConfiguration"/>) whose <c>ClassFullName</c> is the device's type and whose refresh
/// schedule is a valid Quartz cron expression;</item>
/// <item>initialized with its own template, the device reads only parameters the template declares
/// (recorded for devices derived from <see cref="DeviceBase"/>);</item>
/// <item>unless marked <see cref="RequiresHardwareAttribute"/>, the device initializes, starts and stops.</item>
/// </list>
/// </summary>
public static class PluginConformance
{
    /// <summary>How long a device may take to start or stop.</summary>
    public static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Runs every check and throws <see cref="PluginConformanceException"/> listing all failures.</summary>
    /// <param name="pluginType">The plugin class.</param>
    /// <param name="configureServices">Replaces services after the plugin registered them, for example hardware fakes.</param>
    public static void Verify(Type pluginType, Action<IServiceCollection> configureServices = null)
    {
        var failures = Check(pluginType, configureServices);
        if (failures.Count > 0)
            throw new PluginConformanceException(failures);
    }

    /// <summary>Runs every check and returns the failures; empty when the plugin conforms.</summary>
    public static IReadOnlyList<string> Check(Type pluginType, Action<IServiceCollection> configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        var failures = new List<string>();

        if (!typeof(IDevicePlugin).IsAssignableFrom(pluginType) || pluginType.IsAbstract)
        {
            failures.Add($"{pluginType.FullName} isn't a concrete IDevicePlugin.");
            return failures;
        }
        if (pluginType.GetConstructor(Type.EmptyTypes) == null)
        {
            failures.Add($"{pluginType.FullName} needs a public parameterless constructor; the node creates it with Activator.CreateInstance.");
            return failures;
        }

        var entryPoints = GetLoadableTypes(pluginType.Assembly)
            .Where(type => type.GetInterface(nameof(IDevicePlugin)) != null)
            .ToList();
        if (entryPoints.Count != 1)
            failures.Add($"The node loads a plugin assembly only when it has exactly one IDevicePlugin type; {pluginType.Assembly.GetName().Name} has {entryPoints.Count}: {string.Join(", ", entryPoints.Select(type => type.FullName))}.");

        PluginTestHost host;
        try
        {
            host = new PluginTestHost(pluginType, configureServices);
        }
        catch (Exception error)
        {
            failures.Add($"{pluginType.FullName}.Initialize failed: {Describe(error)}");
            return failures;
        }

        using (host)
        {
            var registrations = host.ServiceCollection
                .Where(descriptor => descriptor.ServiceType == typeof(IDevice) && !descriptor.IsKeyedService)
                .ToList();
            if (registrations.Count == 0)
                failures.Add($"{pluginType.FullName} registers no devices.");

            var seen = new HashSet<Type>();
            for (var index = 0; index < registrations.Count; index++)
            {
                IDevice device;
                try
                {
                    device = Create(host.Services, registrations[index]);
                }
                catch (Exception error)
                {
                    failures.Add($"{Describe(registrations[index])} can't be resolved: {Describe(error)}");
                    continue;
                }

                if (!seen.Add(device.GetType()))
                    failures.Add($"{device.GetType().FullName} is registered more than once.");

                try
                {
                    CheckDevice(device, index, failures);
                }
                finally
                {
                    Dispose(device);
                }
            }
        }

        return failures;
    }

    private static void CheckDevice(IDevice device, int index, List<string> failures)
    {
        var name = device.GetType().FullName;
        var requiresHardware = device.GetType().GetCustomAttribute<RequiresHardwareAttribute>(inherit: true) != null;

        DeviceConfiguration template;
        try
        {
            template = DeviceTemplates.Create(device);
        }
        catch (Exception error)
        {
            failures.Add($"{name}: getting the configuration template failed: {Describe(error)}");
            return;
        }

        if (template.ClassFullName != name)
            failures.Add($"{name}: the template's ClassFullName is '{template.ClassFullName}'.");
        if (string.IsNullOrWhiteSpace(template.Name))
            failures.Add($"{name}: the template has no name.");
        if (!string.IsNullOrEmpty(template.RefreshSchedule) && !CronExpression.IsValidExpression(template.RefreshSchedule))
            failures.Add($"{name}: the template's refresh schedule '{template.RefreshSchedule}' isn't a valid Quartz cron expression.");

        var configuration = DeviceConfigurationBuilder.FromTemplate(template).WithId($"conformance-{index}").Build();
        var reads = new List<string>();
        var recorder = device as DeviceBase;
        if (recorder != null)
            recorder.ParameterRead = key => { lock (reads) reads.Add(key); };

        try
        {
            var initialized = false;
            try
            {
                device.Initialize(configuration);
                initialized = true;
            }
            catch (Exception) when (requiresHardware)
            {
                // Expected without the hardware; the parameters read so far are still checked.
            }
            catch (Exception error)
            {
                failures.Add($"{name}: Initialize with the template's values failed: {Describe(error)}. Mark a device that needs hardware or a remote service with [RequiresHardware].");
            }

            if (initialized && !requiresHardware)
                CheckStartStop(device, name, failures);
        }
        finally
        {
            if (recorder != null)
                recorder.ParameterRead = null;
        }

        var declared = template.DeviceParameters?.Keys.ToHashSet(StringComparer.Ordinal) ?? [];
        string[] undeclared;
        lock (reads)
            undeclared = [.. reads.Distinct(StringComparer.Ordinal).Where(key => !declared.Contains(key))];
        foreach (var key in undeclared)
            failures.Add($"{name}: reads parameter '{key}', but its configuration template doesn't declare it.");
    }

    private static void CheckStartStop(IDevice device, string name, List<string> failures)
    {
        try
        {
            if (!Run(PluginTestHost.StartAsync(device), name, "start", failures))
                return;
            if (device.State != DeviceState.Running)
                failures.Add($"{name}: state after start is {device.State} ({device.StateMessage}), expected Running.");

            if (!Run(PluginTestHost.StopAsync(device), name, "stop", failures))
                return;
            if (device.State != DeviceState.Stopped)
                failures.Add($"{name}: state after stop is {device.State} ({device.StateMessage}), expected Stopped.");
        }
        catch (Exception error)
        {
            failures.Add($"{name}: start or stop failed: {Describe(error)}. Mark a device that needs hardware or a remote service with [RequiresHardware].");
        }
    }

    private static bool Run(Task operation, string name, string step, List<string> failures)
    {
        try
        {
            operation.WaitAsync(StartStopTimeout).GetAwaiter().GetResult();
            return true;
        }
        catch (TimeoutException)
        {
            failures.Add($"{name}: {step} took longer than {StartStopTimeout.TotalSeconds:0} s.");
            return false;
        }
    }

    private static IDevice Create(IServiceProvider services, ServiceDescriptor descriptor)
    {
        object instance;
        if (descriptor.ImplementationInstance != null)
            instance = descriptor.ImplementationInstance;
        else if (descriptor.ImplementationFactory != null)
            instance = descriptor.ImplementationFactory(services);
        else
            instance = ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType);

        return instance as IDevice ?? throw new InvalidOperationException("The registration returned no IDevice.");
    }

    private static void Dispose(IDevice device)
    {
        try
        {
            if (device is IAsyncDisposable asyncDisposable)
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else if (device is IDisposable disposable)
                disposable.Dispose();
        }
        catch
        {
            // Disposal isn't part of the contract the node relies on.
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException error)
        {
            return error.Types.Where(type => type != null);
        }
    }

    private static string Describe(ServiceDescriptor descriptor) =>
        descriptor.ImplementationType?.FullName
        ?? descriptor.ImplementationInstance?.GetType().FullName
        ?? "A factory-registered device";

    private static string Describe(Exception error)
    {
        var root = error is AggregateException aggregate ? aggregate.Flatten().InnerException ?? error : error;
        var inner = root.GetBaseException();
        return inner == root ? $"{root.GetType().Name}: {root.Message}" : $"{root.Message} ({inner.GetType().Name}: {inner.Message})";
    }
}
