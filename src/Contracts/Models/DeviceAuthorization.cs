namespace RIoT2.Core.Models
{
    /// <summary>
    /// In a device template (<see cref="DeviceConfiguration.Authorization"/>, Core 2.4.0): the device signs in to a
    /// cloud account with an OAuth 2.0 authorization code, so the UI offers a "Connect" button. The node and the
    /// orchestrator run the flow; the device's secrets and tokens never reach the browser.
    /// </summary>
    public class DeviceAuthorization
    {
        /// <summary>The account's provider, shown on the button, for example <c>Netatmo</c>.</summary>
        public string Provider { get; set; }

        /// <summary>Where the user creates the app whose client id the device uses. Omitted when <c>null</c>.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? SetupUrl { get; set; }

        /// <summary>One-time setup steps for the UI, plain text. Omitted when <c>null</c>.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Instructions { get; set; }
    }

    /// <summary>
    /// Whether a device is linked to its account (<see cref="DeviceStatus.Authorization"/>, Core 2.4.0). Built from
    /// what the device knows, without calling the provider.
    /// </summary>
    public class DeviceAuthorizationStatus
    {
        public DeviceAuthorizationState State { get; set; }

        /// <summary>The connected account, for example its e-mail address. Omitted when <c>null</c>.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Account { get; set; }

        /// <summary>What to do next, or why the credentials stopped working. Omitted when <c>null</c>.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }
    }

    /// <summary>
    /// The body of <c>POST /api/device/{id}/authorization/start</c> on a node (Core 2.4.0). The orchestrator creates
    /// <see cref="State"/> and checks it when the provider redirects the browser back to <see cref="RedirectUri"/>.
    /// </summary>
    public class DeviceAuthorizationStart
    {
        public string RedirectUri { get; set; }
        public string State { get; set; }
    }

    /// <summary>The answer to a start: the provider's sign-in page, which the UI opens (Core 2.4.0).</summary>
    public class DeviceAuthorizationStartResult
    {
        public string AuthorizeUrl { get; set; }
    }

    /// <summary>
    /// The body of <c>POST /api/device/{id}/authorization/complete</c> on a node (Core 2.4.0): the code the provider
    /// returned, and the redirect URI it was issued for, which the token exchange repeats.
    /// </summary>
    public class DeviceAuthorizationCompletion
    {
        public string Code { get; set; }
        public string RedirectUri { get; set; }
    }
}
