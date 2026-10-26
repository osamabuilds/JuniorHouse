using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// The deployment gate (ADR 0011): outside Development and Testing the host refuses to start unless the session cookie scheme is
/// registered and is the default scheme. A deployment without it fails loudly instead of serving
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

        var provider = services.GetRequiredService<IAuthenticationSchemeProvider>();
        if (await provider.GetSchemeAsync(RompAuthorizationExtensions.SessionScheme) is null)
        {
            throw new InvalidOperationException(
                $"Refusing to start: the session cookie scheme '{RompAuthorizationExtensions.SessionScheme}' is not registered and the environment is " +
                $"'{environment.EnvironmentName}'. Staff authentication (Sprint 8, SEC-08) must be in place before anything is deployed (ADR 0011).");
        }

        var defaultScheme = await provider.GetDefaultAuthenticateSchemeAsync();
        if (defaultScheme?.Name != RompAuthorizationExtensions.SessionScheme)
        {
            throw new InvalidOperationException(
                $"Refusing to start: the default authentication scheme is '{defaultScheme?.Name ?? "none"}', not the session cookie scheme " +
                $"'{RompAuthorizationExtensions.SessionScheme}', and the environment is '{environment.EnvironmentName}' (ADR 0011).");
        }
    }
}
