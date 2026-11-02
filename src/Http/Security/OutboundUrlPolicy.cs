using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Security
{
    /// <summary>Why a service makes an outbound HTTP request; an <see cref="IOutboundUrlPolicy"/> decides per purpose.</summary>
    public static class OutboundPurposes
    {
        /// <summary>The orchestrator calling a node's REST API.</summary>
        public const string Node = "node";

        /// <summary>A node, Elsa or a connector calling the orchestrator.</summary>
        public const string Orchestrator = "orchestrator";

        /// <summary>The orchestrator triggering a workflow host (Elsa).</summary>
        public const string Workflow = "workflow";

        /// <summary>Checking or downloading a plugin package.</summary>
        public const string Plugin = "plugin";

        /// <summary>A device calling its hardware, a local bridge or a cloud API (<c>RIoT2.Core.Utils.Web</c>).</summary>
        public const string Device = "device";
    }

    /// <summary>
    /// Decides whether a service may send a request to a URL (design 7.5, feature <c>outbound</c>).
    /// Every outbound client goes through it: <c>IHttpClientFactory</c> clients through
    /// <see cref="OutboundUrlPolicyHandler"/>, and <c>RIoT2.Core.Utils.Web</c> through its
    /// <c>OutboundUrlPolicy</c> property. While the security mode is off the policy allows everything.
    /// </summary>
    public interface IOutboundUrlPolicy
    {
        /// <summary>
        /// Whether <paramref name="url"/> may be called for <paramref name="purpose"/> (one of
        /// <see cref="OutboundPurposes"/>); <paramref name="reason"/> explains a refusal.
        /// </summary>
        bool IsAllowed(Uri url, string purpose, out string? reason);
    }

    /// <summary>The policy while the security mode is off, or the <c>outbound</c> feature isn't enabled: every URL is allowed.</summary>
    public sealed class AllowAllOutboundUrlPolicy : IOutboundUrlPolicy
    {
        /// <summary>The shared instance.</summary>
        public static AllowAllOutboundUrlPolicy Instance { get; } = new AllowAllOutboundUrlPolicy();

        public bool IsAllowed(Uri url, string purpose, out string? reason)
        {
            reason = null;
            return true;
        }
    }

    /// <summary>Thrown instead of sending a request that the <see cref="IOutboundUrlPolicy"/> refused.</summary>
    public sealed class OutboundUrlRejectedException : HttpRequestException
    {
        public OutboundUrlRejectedException(Uri url, string purpose, string? reason)
            : base($"The outbound URL policy refused {purpose} request to {url.GetLeftPart(UriPartial.Path)}" +
                (string.IsNullOrEmpty(reason) ? "." : ": " + reason))
        {
            Url = url;
            Purpose = purpose;
        }

        public Uri Url { get; }

        public string Purpose { get; }
    }

    /// <summary>Checks every request of an <c>IHttpClientFactory</c> client against an <see cref="IOutboundUrlPolicy"/>.</summary>
    public sealed class OutboundUrlPolicyHandler : DelegatingHandler
    {
        private readonly IOutboundUrlPolicy _policy;
        private readonly string _purpose;

        public OutboundUrlPolicyHandler(IOutboundUrlPolicy policy, string purpose)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _purpose = purpose ?? throw new ArgumentNullException(nameof(purpose));
        }

        /// <summary>Wraps <paramref name="innerHandler"/>, for clients built without <c>IHttpClientFactory</c> (gRPC channels).</summary>
        public OutboundUrlPolicyHandler(IOutboundUrlPolicy policy, string purpose, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _purpose = purpose ?? throw new ArgumentNullException(nameof(purpose));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Check(_policy, request.RequestUri, _purpose);
            return base.SendAsync(request, cancellationToken);
        }

        /// <summary>Throws <see cref="OutboundUrlRejectedException"/> when <paramref name="policy"/> refuses <paramref name="url"/>.</summary>
        public static void Check(IOutboundUrlPolicy policy, Uri? url, string purpose)
        {
            // A relative or missing URL fails in HttpClient itself, with its usual message.
            if (url == null || !url.IsAbsoluteUri)
                return;
            if (!policy.IsAllowed(url, purpose, out var reason))
                throw new OutboundUrlRejectedException(url, purpose, reason);
        }
    }
}
