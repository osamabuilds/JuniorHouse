using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>
/// The names of the three session cookies. The <c>__Secure-</c> prefix makes a browser refuse the cookie unless it is Secure; it is
/// dropped only when <see cref="IdentityOptions.AllowInsecureCookies"/> is on, which start-up refuses outside Development/Testing.
/// </summary>
public sealed record SessionCookieNames(string Access, string Refresh, string Pending)
{
    private static readonly SessionCookieNames Secure = new("__Secure-romp-acs", "__Secure-romp-rfs", "__Secure-romp-pnd");
    private static readonly SessionCookieNames Insecure = new("romp-acs", "romp-rfs", "romp-pnd");

    public static SessionCookieNames For(bool allowInsecure) => allowInsecure ? Insecure : Secure;
}

/// <summary>
/// Writes the session cookies (sessions AC-1): every one is <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Strict</c> and host-only. The
/// refresh value is sent only to the refresh endpoint and the pending value only to the MFA endpoint, so the long-lived and the
/// half-signed-in values travel as little as possible.
/// </summary>
public sealed class SessionCookieWriter(IOptions<IdentityOptions> options)
{
    private const string AccessPath = "/api";
    private const string RefreshPath = "/api/auth/refresh";
    private const string PendingPath = "/api/auth/mfa";

    private readonly bool _insecure = options.Value.AllowInsecureCookies;

    private SessionCookieNames Names => SessionCookieNames.For(_insecure);

    public void WriteSession(HttpResponse response, IssuedTokens tokens, DateTimeOffset accessExpiresAt, DateTimeOffset refreshExpiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(tokens);

        response.Cookies.Append(Names.Access, tokens.Access.Reveal(), Options(AccessPath, accessExpiresAt));
        response.Cookies.Append(Names.Refresh, tokens.Refresh.Reveal(), Options(RefreshPath, refreshExpiresAt));
    }

    public void WritePending(HttpResponse response, SecretString token, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(token);

        response.Cookies.Append(Names.Pending, token.Reveal(), Options(PendingPath, expiresAt));
    }

    /// <summary>Expires all three cookies. Each is cleared on the path it was set on, or the browser keeps it.</summary>
    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var past = DateTimeOffset.UnixEpoch;
        response.Cookies.Append(Names.Access, string.Empty, Options(AccessPath, past));
        response.Cookies.Append(Names.Refresh, string.Empty, Options(RefreshPath, past));
        response.Cookies.Append(Names.Pending, string.Empty, Options(PendingPath, past));
    }

    /// <summary>Responses that carry or clear credentials must never be stored by the browser or a shared cache.</summary>
    public static void NoStore(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Headers.CacheControl = "no-store";
    }

    private CookieOptions Options(string path, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = !_insecure,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
        IsEssential = true, // a sign-in cookie is not optional; consent rules do not apply
    };
}

/// <summary><c>Identity:Sessions:*</c> - how long each part of a session lives, in minutes (sessions AC-2, AC-5).</summary>
public sealed class SessionLifetimeOptions
{
    public const string Section = "Identity:Sessions";

    /// <summary>The longest absolute limit accepted: one week. A staff session is a working day, not a standing login.</summary>
    public const int MaxAbsoluteMinutes = 7 * 24 * 60;

    public int AccessMinutes { get; set; } = 15;

    public int IdleMinutes { get; set; } = 30;

    public int AbsoluteMinutes { get; set; } = 12 * 60;

    public SessionLifetimes ToLifetimes() =>
        new(TimeSpan.FromMinutes(AccessMinutes), TimeSpan.FromMinutes(IdleMinutes), TimeSpan.FromMinutes(AbsoluteMinutes));
}

public sealed class SessionLifetimeOptionsValidator : IValidateOptions<SessionLifetimeOptions>
{
    public ValidateOptionsResult Validate(string? name, SessionLifetimeOptions options)
    {
        if (options.AccessMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Identity:Sessions:AccessMinutes must be greater than zero.");
        }

        if (options.IdleMinutes < options.AccessMinutes)
        {
            return ValidateOptionsResult.Fail("Identity:Sessions:IdleMinutes cannot be shorter than AccessMinutes.");
        }

        if (options.AbsoluteMinutes < options.IdleMinutes)
        {
            return ValidateOptionsResult.Fail("Identity:Sessions:AbsoluteMinutes cannot be shorter than IdleMinutes.");
        }

        return options.AbsoluteMinutes > SessionLifetimeOptions.MaxAbsoluteMinutes
            ? ValidateOptionsResult.Fail($"Identity:Sessions:AbsoluteMinutes cannot exceed {SessionLifetimeOptions.MaxAbsoluteMinutes} (one week).")
            : ValidateOptionsResult.Success;
    }
}

public static class SessionServiceCollectionExtensions
{
    /// <summary>Binds <c>Identity:Sessions</c>, validates it when the host starts, and exposes the result as <see cref="SessionLifetimes"/>.</summary>
    public static IServiceCollection AddSessionLifetimes(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SessionLifetimeOptions>().Bind(configuration.GetSection(SessionLifetimeOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SessionLifetimeOptions>, SessionLifetimeOptionsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SessionLifetimeOptions>>().Value.ToLifetimes());
        return services;
    }
}
