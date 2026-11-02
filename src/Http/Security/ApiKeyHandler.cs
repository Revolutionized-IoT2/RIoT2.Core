using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Security
{
    /// <summary>
    /// The service-to-service API key (design 7.5): a service with <c>RIOT2_API_KEY</c> set sends it in
    /// <c>X-RIoT2-Key</c> on every call to another RIoT2 service. Servers check it only when the
    /// security mode is on; while it is off the header is sent and ignored.
    /// </summary>
    public static class ServiceApiKey
    {
        /// <summary>The request header (and gRPC metadata key, lower case) that carries the key.</summary>
        public const string HeaderName = "X-RIoT2-Key";

        /// <summary>The setting that holds a service's own key.</summary>
        public const string Variable = "RIOT2_API_KEY";

        /// <summary>Adds the key to <paramref name="request"/> unless it is empty or the request already has one.</summary>
        public static void Apply(HttpRequestMessage request, string? apiKey)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(apiKey) || request.Headers.Contains(HeaderName))
                return;
            request.Headers.TryAddWithoutValidation(HeaderName, apiKey);
        }
    }

    /// <summary>
    /// Sends <see cref="ServiceApiKey.HeaderName"/> on every request when a key is configured. Add it
    /// only to clients that call other RIoT2 services, never to clients that call devices or the
    /// Internet.
    /// </summary>
    public sealed class ApiKeyHandler : DelegatingHandler
    {
        private readonly string? _apiKey;

        /// <param name="apiKey">The key; <c>null</c> or empty sends nothing.</param>
        public ApiKeyHandler(string? apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>Wraps <paramref name="innerHandler"/>, for clients built without <c>IHttpClientFactory</c> (gRPC channels).</summary>
        public ApiKeyHandler(string? apiKey, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            _apiKey = apiKey;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ServiceApiKey.Apply(request, _apiKey);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
