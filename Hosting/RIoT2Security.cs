using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Configuration;
using RIoT2.Core.Models;
using RIoT2.Core.Security;
using RIoT2.Core.Utils;

namespace RIoT2.Core.Hosting;

/// <summary>
/// The optional security mode's seams for an ASP.NET Core service (ADR 0004, design 7.5 phase S0):
/// the <c>Viewer</c>, <c>Operator</c> and <c>Admin</c> policies with the <c>LocalTrust</c> scheme,
/// CORS from <c>RIOT2_CORS_ORIGINS</c>, the outbound URL policy, and <c>GET /api/security/info</c>.
/// With the mode off, which is the only mode this version accepts, they change nothing: every
/// request is <c>local-admin</c>, any origin is allowed and every URL may be called.
/// </summary>
public static class RIoT2Security
{
    /// <summary>The route of the anonymous security description (<see cref="SecurityInfo"/>).</summary>
    public const string InfoRoute = "/api/security/info";

    /// <summary>
    /// Registers <paramref name="options"/>, the <see cref="SecurityPolicies.LocalTrustScheme"/> scheme,
    /// the three policies, the default CORS policy and the <see cref="IOutboundUrlPolicy"/>.
    /// </summary>
    /// <param name="defaultScheme">
    /// Makes the local-trust scheme the default. A host with its own authentication (Elsa) passes
    /// <c>false</c>; the policies name the scheme, so they still use it.
    /// </param>
    /// <param name="configureCors">Adds to the default CORS policy, for example exposed headers.</param>
    public static IServiceCollection AddRIoT2Security(this IServiceCollection services, SecurityOptions options,
        bool defaultScheme = true, Action<CorsPolicyBuilder>? configureCors = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        var authentication = defaultScheme ? services.AddAuthentication(SecurityPolicies.LocalTrustScheme) : services.AddAuthentication();
        authentication.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, LocalTrustAuthenticationHandler>(
            SecurityPolicies.LocalTrustScheme, null);
        services.AddAuthorization(authorization =>
        {
            foreach (var policy in new[] { SecurityPolicies.Viewer, SecurityPolicies.Operator, SecurityPolicies.Admin })
                authorization.AddPolicy(policy, builder => builder
                    .AddAuthenticationSchemes(SecurityPolicies.LocalTrustScheme)
                    .RequireAuthenticatedUser()
                    .RequireRole(SecurityPolicies.RolesFor(policy)));
        });
        services.AddCors(cors => cors.AddDefaultPolicy(policy =>
        {
            ConfigureCors(policy, options);
            configureCors?.Invoke(policy);
        }));
        services.TryAddSingleton<IOutboundUrlPolicy>(AllowAllOutboundUrlPolicy.Instance);
        return services;
    }

    /// <summary>
    /// Any origin while the mode is off or the <c>api</c> feature isn't enforced, as before; otherwise
    /// only <see cref="SecurityOptions.CorsOrigins"/>, with credentials (the login cookie).
    /// </summary>
    public static void ConfigureCors(CorsPolicyBuilder policy, SecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(options);
        policy.AllowAnyMethod().AllowAnyHeader();
        if (options.IsEnabled(SecurityFeatures.Api) && options.CorsOrigins.Count > 0)
            policy.WithOrigins(options.CorsOrigins.ToArray()).AllowCredentials();
        else
            policy.AllowAnyOrigin();
    }

    /// <summary>
    /// Adds CORS, authentication and authorization to the pipeline, in that order, then points
    /// <see cref="Web.OutboundUrlPolicy"/> at the registered <see cref="IOutboundUrlPolicy"/> and logs the mode.
    /// </summary>
    /// <param name="pipeline">
    /// <c>false</c> for a host that adds those middlewares itself (Elsa): only the policy is set and the
    /// mode logged.
    /// </param>
    public static IApplicationBuilder UseRIoT2Security(this IApplicationBuilder app, bool pipeline = true)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (pipeline)
        {
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
        }
        var services = app.ApplicationServices;
        Web.OutboundUrlPolicy = services.GetRequiredService<IOutboundUrlPolicy>();
        var options = services.GetRequiredService<SecurityOptions>();
        services.GetService<ILoggerFactory>()?.CreateLogger("RIoT2.Security").LogInformation(
            "Security mode {Mode}: nothing is enforced; every request runs as {User} (design 7.5). API key {ApiKey}.",
            SecurityOptions.ToName(options.Mode), SecurityPolicies.LocalUserName, options.ApiKey == null ? "not set" : "set");
        return app;
    }

    /// <summary>Maps the anonymous <c>GET /api/security/info</c>.</summary>
    public static RouteHandlerBuilder MapRIoT2SecurityInfo(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGet(InfoRoute, (SecurityOptions options) => Results.Content(Json.Serialize(Describe(options)), "application/json"))
            .AllowAnonymous()
            .WithName("GetSecurityInfo")
            .WithTags("Security")
            .Produces<SecurityInfo>();
    }

    /// <summary>What <c>GET /api/security/info</c> reports for <paramref name="options"/>.</summary>
    public static SecurityInfo Describe(SecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SecurityInfo
        {
            Mode = SecurityOptions.ToName(options.Mode),
            Features = options.EnforcedFeatures.ToList(),
            LoginRequired = options.IsEnabled(SecurityFeatures.Api),
            Realtime = options.IsEnabled(SecurityFeatures.Realtime) ? SecurityInfo.RealtimeGateway : SecurityInfo.RealtimeMqtt
        };
    }

    /// <summary>
    /// The endpoints that have neither a policy (<c>[Authorize(Policy = …)]</c> or
    /// <c>RequireAuthorization(policy)</c>) nor <c>[AllowAnonymous]</c>, by display name. Tests assert it
    /// is empty, so every new endpoint gets a policy (design 7.5).
    /// </summary>
    public static IReadOnlyList<string> EndpointsWithoutPolicy(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null
                && !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => !string.IsNullOrEmpty(data.Policy)))
            .Select(endpoint => endpoint.DisplayName ?? endpoint.ToString() ?? "?")
            .Distinct()
            .ToList();
    }

}

/// <summary>
/// Gives every MVC action that declares neither a policy nor <c>[AllowAnonymous]</c> one by its HTTP
/// methods: <c>GET</c>/<c>HEAD</c> only is <c>Viewer</c>, anything else <c>Operator</c> (design 7.5).
/// The node adds it for plugin controllers, which are built separately.
/// </summary>
public sealed class DefaultPolicyConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);
        foreach (var controller in application.Controllers)
        {
            if (Declares(controller.Attributes))
                continue;
            foreach (var action in controller.Actions)
            {
                if (Declares(action.Attributes))
                    continue;
                var policy = PolicyFor(action.Attributes.OfType<IActionHttpMethodProvider>().SelectMany(m => m.HttpMethods).ToList());
                foreach (var selector in action.Selectors)
                    selector.EndpointMetadata.Add(new AuthorizeAttribute(policy));
            }
        }
    }

    /// <summary>The default policy of an action that accepts <paramref name="methods"/> (all methods when empty).</summary>
    public static string PolicyFor(IReadOnlyCollection<string> methods) =>
        methods.Count > 0 && methods.All(m => HttpMethods.IsGet(m) || HttpMethods.IsHead(m)) ? SecurityPolicies.Viewer : SecurityPolicies.Operator;

    private static bool Declares(IReadOnlyList<object> attributes) =>
        attributes.OfType<IAllowAnonymous>().Any() || attributes.OfType<IAuthorizeData>().Any(data => !string.IsNullOrEmpty(data.Policy));
}
