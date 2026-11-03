using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// Where the orchestrator's API is: the <c>apiBaseUrl</c> of the last configuration command the connector received.
/// <see cref="ConnectorMqttService"/> sets it; the template catalog and <see cref="IRiot2CommandClient"/> use it.
/// </summary>
public sealed class OrchestratorEndpoint
{
    private string? _baseUrl;

    /// <summary>The orchestrator's base URL without a trailing slash, or <c>null</c> before the first configuration command.</summary>
    public string? BaseUrl
    {
        get => Volatile.Read(ref _baseUrl);
        set => Volatile.Write(ref _baseUrl, string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('/'));
    }
}

/// <summary>How a command is sent: its time-to-live and whether to wait for the node's result.</summary>
public sealed class CommandSendOptions
{
    /// <summary>The time-to-live (1 s to 24 h); the orchestrator's default when <c>null</c>.</summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>Wait until the delivery is final (at the latest at its expiry) instead of returning once it is queued.</summary>
    public bool Wait { get; init; }
}

/// <summary>A command delivery as <c>POST /api/v2/commands</c> returns it (design 7.1).</summary>
public sealed class CommandDeliveryInfo
{
    public string CorrelationId { get; init; } = "";
    public string? CommandId { get; init; }
    public string? NodeId { get; init; }

    /// <summary><c>pending</c>, <c>sent</c>, <c>executed</c>, <c>failed</c>, <c>unknownCommand</c>, <c>duplicate</c> or <c>expired</c>.</summary>
    public string Status { get; init; } = "";
    public string? Error { get; init; }
    public int Attempts { get; init; }
    public bool IsFinal { get; init; }
}

/// <summary>The orchestrator refused the command itself (400: invalid, 404: no node owns the command id): don't retry.</summary>
public sealed class CommandRejectedException : Exception
{
    public CommandRejectedException(HttpStatusCode statusCode, string message) : base(message) => StatusCode = statusCode;

    public HttpStatusCode StatusCode { get; }
}

/// <summary>
/// Sends commands into RIoT2 for bidirectional connectors (design 7.3 decision 6). It calls the orchestrator's
/// <c>POST /api/v2/commands</c>, so a connector's commands go through the outbox, with its time-to-live and results,
/// like the UI's and Elsa's. Connectors never publish command topics themselves.
/// </summary>
public interface IRiot2CommandClient
{
    /// <summary>Whether the orchestrator's URL is known (after the first configuration command).</summary>
    bool IsReady { get; }

    /// <summary>
    /// Queues the command <paramref name="commandId"/> with <paramref name="value"/>. Throws
    /// <see cref="CommandRejectedException"/> when the orchestrator refuses it, <see cref="InvalidOperationException"/>
    /// before the orchestrator's URL is known, and <see cref="HttpRequestException"/> when it doesn't answer.
    /// </summary>
    Task<CommandDeliveryInfo> SendAsync(string commandId, ValueModel value, CommandSendOptions? options = null, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IRiot2CommandClient"/>
public sealed class Riot2CommandClient : IRiot2CommandClient
{
    private readonly HttpClient _http;
    private readonly OrchestratorEndpoint _endpoint;

    /// <param name="http">The client for the orchestrator: <c>X-RIoT2-Key</c> and the outbound URL policy are its handlers.</param>
    public Riot2CommandClient(HttpClient http, OrchestratorEndpoint endpoint)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public bool IsReady => _endpoint.BaseUrl != null;

    public async Task<CommandDeliveryInfo> SendAsync(string commandId, ValueModel value, CommandSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(value);
        var baseUrl = _endpoint.BaseUrl
            ?? throw new InvalidOperationException("The orchestrator's URL is not known yet; it comes with its configuration command.");

        var body = new JsonObject
        {
            ["id"] = commandId,
            ["value"] = JsonNode.Parse(value.ToJson())
        };
        if (options?.TimeToLive is { } ttl)
            body["ttlSeconds"] = (int)Math.Clamp(Math.Ceiling(ttl.TotalSeconds), 1, 86400);
        if (options?.Wait == true)
            body["wait"] = true;

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(baseUrl + "/api/v2/commands", content, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            throw new CommandRejectedException(response.StatusCode, $"The orchestrator refused command {commandId} ({(int)response.StatusCode}): {Detail(text)}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"The orchestrator answered {(int)response.StatusCode} to command {commandId}: {Detail(text)}", null, response.StatusCode);
        return Json.Deserialize<CommandDeliveryInfo>(text)
            ?? throw new InvalidDataException($"The orchestrator returned no delivery for command {commandId}.");
    }

    // A problem response's detail, or the text itself.
    private static string Detail(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is JsonObject problem && problem["detail"] is JsonValue detail)
                return detail.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            // Plain text.
        }
        return text.Length > 300 ? text[..300] : text;
    }
}

/// <summary>
/// Told when the connector has loaded a new set of templates, for example to publish discovery messages. Register
/// implementations as <c>IConnectorTemplateObserver</c> singletons; a failure is logged and doesn't stop the others.
/// </summary>
public interface IConnectorTemplateObserver
{
    Task TemplatesLoadedAsync(TemplateSnapshot templates, CancellationToken cancellationToken);
}
