using RIoT2.Core.Utils;

namespace RIoT2.Connector.Sdk;

/// <summary>A report, command or variable template as the orchestrator lists it, with the names a connector writes.</summary>
public sealed class ConnectorTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string Node { get; set; } = string.Empty;

    /// <summary>The device's id (Core 2.1.0); empty for variables.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>The node's id (Core 2.1.0); empty for variables.</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>The device's address of the report or command (Core 2.1.0).</summary>
    public string? Address { get; set; }

    /// <summary>The value type (Core 2.1.0).</summary>
    public RIoT2.Core.ValueType Type { get; set; }

    /// <summary>A report template's parameters, for example <c>unit</c> and <c>precision</c> (Core 2.1.0).</summary>
    public Dictionary<string, string>? Parameters { get; set; }

    /// <summary>
    /// What the value is (Core 2.2.0): its type, unit, meaning and range, when the device declares it; <c>null</c>
    /// for variables and devices that don't.
    /// </summary>
    public RIoT2.Core.Models.ValueSchema? Schema { get; set; }
}

/// <summary>One complete set of templates; replaced as a whole, never partly.</summary>
public sealed record TemplateSnapshot(
    IReadOnlyList<ConnectorTemplate> Reports,
    IReadOnlyList<ConnectorTemplate> Commands,
    IReadOnlyList<ConnectorTemplate> Variables)
{
    /// <summary>The report template with the id, or the variable's (variables are reported like reports).</summary>
    public ConnectorTemplate? FindReport(string id) =>
        Reports.FirstOrDefault(t => t.Id == id) ?? Variables.FirstOrDefault(t => t.Id == id);

    public ConnectorTemplate? FindCommand(string id) => Commands.FirstOrDefault(t => t.Id == id);
}

/// <summary>The templates the connector maps messages with, loaded from the orchestrator.</summary>
public interface ITemplateCatalog
{
    /// <summary>The last complete load, or <c>null</c> before the first one succeeded.</summary>
    TemplateSnapshot? Snapshot { get; }

    /// <summary>
    /// Loads the report, command and variable templates from the orchestrator. The snapshot is replaced only when all
    /// three load and are valid; otherwise the last good one stays and this throws.
    /// </summary>
    Task LoadAsync(string orchestratorBaseUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads templates from <c>/api/nodes/report/templates</c>, <c>/api/nodes/command/templates</c> and
/// <c>/api/nodes/variable/templates</c> when the orchestrator sends the configuration command.
/// </summary>
public sealed class TemplateCatalog : ITemplateCatalog, IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private TemplateSnapshot? _snapshot;

    /// <param name="http">The client for the orchestrator: <c>X-RIoT2-Key</c> and the outbound URL policy are its handlers.</param>
    public TemplateCatalog(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    public TemplateSnapshot? Snapshot => Volatile.Read(ref _snapshot);

    public async Task LoadAsync(string orchestratorBaseUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestratorBaseUrl);
        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var root = orchestratorBaseUrl.TrimEnd('/');
            var reports = LoadListAsync(root + "/api/nodes/report/templates", cancellationToken);
            var commands = LoadListAsync(root + "/api/nodes/command/templates", cancellationToken);
            var variables = LoadListAsync(root + "/api/nodes/variable/templates", cancellationToken);
            await Task.WhenAll(reports, commands, variables).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, new TemplateSnapshot(
                (await reports.ConfigureAwait(false)).AsReadOnly(),
                (await commands.ConfigureAwait(false)).AsReadOnly(),
                (await variables.ConfigureAwait(false)).AsReadOnly()));
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public void Dispose() => _loadGate.Dispose();

    private async Task<List<ConnectorTemplate>> LoadListAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var templates = Json.Deserialize<List<ConnectorTemplate>>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (templates == null || templates.Any(t => t == null || string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.Name)))
            throw new InvalidDataException($"Invalid template response from {url}.");
        return templates;
    }
}
