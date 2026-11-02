using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RIoT2.Core.Security
{
    /// <summary>The authorization policies every RIoT2 endpoint carries (design 7.5), and the roles behind them.</summary>
    public static class SecurityPolicies
    {
        /// <summary>Reads: every <c>GET</c>.</summary>
        public const string Viewer = "Viewer";

        /// <summary>Runs commands and triggers workflows.</summary>
        public const string Operator = "Operator";

        /// <summary>Changes configuration, nodes, variables, dashboards and Matter, and runs backups and restores.</summary>
        public const string Admin = "Admin";

        /// <summary>The scheme that signs every request in as <see cref="LocalUserName"/> while the security mode is off.</summary>
        public const string LocalTrustScheme = "RIoT2LocalTrust";

        /// <summary>The user every request runs as while the security mode is off; audit log lines name it.</summary>
        public const string LocalUserName = "local-admin";

        /// <summary>The roles that satisfy <paramref name="policy"/>: a higher role includes the lower ones.</summary>
        public static string[] RolesFor(string policy) => policy switch
        {
            Viewer => new[] { Viewer, Operator, Admin },
            Operator => new[] { Operator, Admin },
            Admin => new[] { Admin },
            _ => throw new System.ArgumentOutOfRangeException(nameof(policy), policy, "Not a RIoT2 policy.")
        };
    }

    /// <summary>
    /// Authenticates every request as <see cref="SecurityPolicies.LocalUserName"/> with every role, so
    /// all policies pass while the security mode is off (design 7.5, phase S0). It never fails and
    /// never challenges.
    /// </summary>
    public sealed class LocalTrustAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public LocalTrustAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, SecurityPolicies.LocalUserName),
                new Claim(ClaimTypes.Role, SecurityPolicies.Viewer),
                new Claim(ClaimTypes.Role, SecurityPolicies.Operator),
                new Claim(ClaimTypes.Role, SecurityPolicies.Admin)
            ], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
