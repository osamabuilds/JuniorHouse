using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// The deployment gate (ADR 0011): outside Development and Testing the host refuses to start unless a real authentication
/// scheme has been registered. Staff login (S8) registers one; until then a deployment fails loudly instead of serving
/// every endpoint to anyone.
/// </summary>
public static class SecurityGate
{
    public static async Task VerifyAsync(IServiceProvider services, IHostEnvironment environment)
    {
        if (RompAuthorizationExtensions.IsDevelopmentLike(environment))
        {
            return;
        }

        var schemes = await services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name != RompAuthorizationExtensions.DevelopmentScheme))
        {
            throw new InvalidOperationException(
                $"Refusing to start: no authentication scheme is registered and the environment is '{environment.EnvironmentName}'. " +
                "Staff authentication (Sprint 8, SEC-08) must be in place before anything is deployed (ADR 0011).");
        }
    }
}
