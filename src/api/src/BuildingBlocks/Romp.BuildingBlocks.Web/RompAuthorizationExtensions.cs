using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// Authorization wiring until staff login exists (S8). The policies are real; the only authentication scheme is a
/// development handler that signs every caller in as a dev user and is registered ONLY in Development and Testing. In any
/// other environment no scheme exists, so every secured endpoint refuses callers, and <see cref="SecurityGate"/> stops the
/// host from starting at all (ADR 0011).
/// </summary>
public static class RompAuthorizationExtensions
{
    public const string DevelopmentScheme = "DevelopmentStaff";

    /// <summary>The scheme of the Identity module's session cookie handler. The module registers the handler; this layer only names it.</summary>
    public const string SessionScheme = "RompSession";

    public static IServiceCollection AddRompAuthorization(this IServiceCollection services, IHostEnvironment environment)
    {
        var authentication = services.AddAuthentication();
        if (IsDevelopmentLike(environment))
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevelopmentStaffHandler>(DevelopmentScheme, _ => { });
            services.AddSingleton<IAuthorizationHandler, DevelopmentPermissionHandler>();
        }

        // Real authentication (the session cookie) is the default everywhere except a Development/Testing host that has not asked for
        // it with Identity:UseRealAuthentication; only there does the development handler stand in.
        services.AddOptions<AuthenticationOptions>().Configure<IOptions<IdentityOptions>>((options, identity) =>
            options.DefaultScheme = IdentityOptions.IsRealAuthentication(identity.Value, environment) ? SessionScheme : DevelopmentScheme);

        // Permissions are decided by a handler per requirement (the Identity module's, or the development one); the one named policy left
        // is the explicit "open to everyone".
        services.AddAuthorizationBuilder().AddPolicy(Policies.Public, policy => policy.RequireAssertion(_ => true));
        return services;
    }

    public static bool IsDevelopmentLike(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing");

    private sealed class DevelopmentStaffHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "dev-staff")],
                DevelopmentScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), DevelopmentScheme)));
        }
    }
}
