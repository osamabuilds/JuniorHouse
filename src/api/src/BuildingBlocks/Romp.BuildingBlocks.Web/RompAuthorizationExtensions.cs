using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
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

    public static IServiceCollection AddRompAuthorization(this IServiceCollection services, IHostEnvironment environment)
    {
        var authentication = services.AddAuthentication();
        if (IsDevelopmentLike(environment))
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevelopmentStaffHandler>(DevelopmentScheme, _ => { });
            services.Configure<AuthenticationOptions>(options => options.DefaultScheme = DevelopmentScheme);
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Staff, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole("Admin"))
            .AddPolicy(Policies.Inventory, policy => policy.RequireAuthenticatedUser().RequireRole("Inventory", "Admin"))
            .AddPolicy(Policies.Catalogue, policy => policy.RequireAuthenticatedUser().RequireRole("Catalogue", "Admin"));
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
                [new Claim(ClaimTypes.Name, "dev-staff"), new Claim(ClaimTypes.Role, "Staff"), new Claim(ClaimTypes.Role, "Admin")],
                DevelopmentScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), DevelopmentScheme)));
        }
    }
}
