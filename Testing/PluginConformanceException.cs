namespace RIoT2.Core.Devices.Testing;

/// <summary>Thrown by <see cref="PluginConformance.Verify"/> with every rule the plugin breaks.</summary>
public sealed class PluginConformanceException : Exception
{
    public PluginConformanceException(IReadOnlyList<string> failures)
        : base(BuildMessage(failures)) => Failures = failures;

    public IReadOnlyList<string> Failures { get; }

    private static string BuildMessage(IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return $"The plugin breaks {failures.Count} conformance rule(s):" + Environment.NewLine +
            string.Join(Environment.NewLine, failures.Select(failure => "- " + failure));
    }
}
