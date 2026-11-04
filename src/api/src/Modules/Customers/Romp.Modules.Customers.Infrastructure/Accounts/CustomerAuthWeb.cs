using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

/// <summary>
/// <c>Customers:Auth:*</c>. Real customer sign-in is always on outside Development and Testing; there it is on only when
/// <see cref="UseRealSignIn"/> is true, so the development stand-in (ADR 0021) can never be what lets a customer in on a server.
/// </summary>
public sealed class CustomerAuthOptions
{
    public const string Section = "Customers:Auth";

    public const int MinKeyBytes = 32;

    public bool? UseRealSignIn { get; set; }

    /// <summary>Drops the <c>Secure</c> flag and the <c>__Secure-</c> cookie prefix, for plain http on a developer machine. Refused elsewhere.</summary>
    public bool AllowInsecureCookies { get; set; }

    /// <summary>Base64 of at least 32 random bytes behind the anti-forgery value; a deployment secret. Optional in Development and Testing only.</summary>
    public string? CsrfKey { get; set; }

    public static bool IsReal(CustomerAuthOptions options, IHostEnvironment environment) =>
        !RompAuthorizationExtensions.IsDevelopmentLike(environment) || options.UseRealSignIn == true;
}

public sealed class CustomerAuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<CustomerAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, CustomerAuthOptions options)
    {
        var developmentLike = RompAuthorizationExtensions.IsDevelopmentLike(environment);
        if (!developmentLike && options.AllowInsecureCookies)
        {
            return ValidateOptionsResult.Fail($"{CustomerAuthOptions.Section}:AllowInsecureCookies is only allowed in Development and Testing.");
        }

        if (string.IsNullOrEmpty(options.CsrfKey))
        {
            return developmentLike
                ? ValidateOptionsResult.Success // the process makes up its own key; restarting makes the browser's tokens out of date
                : ValidateOptionsResult.Fail($"{CustomerAuthOptions.Section}:CsrfKey is required outside Development and Testing.");
        }

        var buffer = new byte[options.CsrfKey.Length];
        if (!Convert.TryFromBase64String(options.CsrfKey, buffer, out var written))
        {
            return ValidateOptionsResult.Fail($"{CustomerAuthOptions.Section}:CsrfKey must be base64.");
        }

        return written < CustomerAuthOptions.MinKeyBytes
            ? ValidateOptionsResult.Fail($"{CustomerAuthOptions.Section}:CsrfKey must hold at least {CustomerAuthOptions.MinKeyBytes} bytes.")
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// The customer session cookies (ACT-11, ACT-12): <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Lax</c> (Q-ACT-5: the storefront links in from
/// email and other sites, and the anti-forgery header protects unsafe requests), host-only, on the storefront origin. The <c>__Secure-</c>
/// prefix makes a browser refuse the cookie unless it is Secure.
/// </summary>
public sealed class CustomerCookies(IOptions<CustomerAuthOptions> options)
{
    /// <summary>The guest-session cookie the cart (S11) sets; read here only to hand it to the sign-in hook so a guest basket can be merged.</summary>
    public const string GuestCookie = "romp-guest";

    private readonly bool _insecure = options.Value.AllowInsecureCookies;

    public string AccessName => _insecure ? "rc-access" : "__Secure-rc-access";

    public string RefreshName => _insecure ? "rc-refresh" : "__Secure-rc-refresh";

    public void WriteSession(HttpResponse response, StartedSession session)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(session);

        response.Cookies.Append(AccessName, session.Tokens.Access.Reveal(), Options(session.AccessExpiresAt));
        response.Cookies.Append(RefreshName, session.Tokens.Refresh.Reveal(), Options(session.RefreshExpiresAt));
    }

    public string? ReadAccess(HttpRequest request) => request.Cookies[AccessName];

    public string? ReadRefresh(HttpRequest request) => request.Cookies[RefreshName];

    public static string? ReadGuestSession(HttpRequest request)
    {
        var value = request.Cookies[GuestCookie];
        return string.IsNullOrEmpty(value) || value.Length > 128 ? null : value;
    }

    /// <summary>Forgets the guest cookie once its basket has been handed to the account on sign-in or registration (FR-35).</summary>
    public static void ClearGuest(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(GuestCookie, new CookieOptions { Path = "/" });
    }

    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(AccessName, string.Empty, Options(DateTimeOffset.UnixEpoch));
        response.Cookies.Append(RefreshName, string.Empty, Options(DateTimeOffset.UnixEpoch));
    }

    private CookieOptions Options(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = !_insecure,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true, // a sign-in cookie is not optional; consent rules do not apply
    };
}

/// <summary>The customer as the rest of the host sees them: who and which session.</summary>
public static class CustomerSessionPrincipal
{
    public const string Scheme = "CustomerSession";

    public const string SessionIdClaim = "rc_session";

    public static ClaimsPrincipal Create(AuthenticatedCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(SessionIdClaim, customer.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
            Scheme);
        return new ClaimsPrincipal(identity);
    }

    public static long? SessionId(ClaimsPrincipal user)
    {
        var value = user.Identities.FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == Scheme)?.FindFirst(SessionIdClaim)?.Value;
        return long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}

/// <summary>Authenticates a request from its access cookie (ACT-11). No cookie, or a session that cannot be used, means no result, which the pipeline turns into 401.</summary>
internal sealed class CustomerSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISender sender, CustomerCookies cookies)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = cookies.ReadAccess(Request);
        if (string.IsNullOrEmpty(value))
        {
            return AuthenticateResult.NoResult();
        }

        var customer = await sender.Send(new AuthenticateCustomerSessionQuery(value), Context.RequestAborted);
        return customer is null
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Success(new AuthenticationTicket(CustomerSessionPrincipal.Create(customer), Scheme.Name));
    }
}

/// <summary>
/// The per-session anti-forgery value: <c>HMAC-SHA256(serverKey, sessionId)</c>, base64url. A page script reads it from
/// <c>GET /api/customers/auth/session</c> and sends it in <see cref="CustomerCsrfFilter.HeaderName"/>; a site that merely makes the browser
/// send the cookie cannot read it, so it cannot produce it. Nothing is stored.
/// </summary>
public sealed class CustomerCsrfTokens
{
    private readonly byte[] _key;

    public CustomerCsrfTokens(IOptions<CustomerAuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _key = string.IsNullOrEmpty(options.Value.CsrfKey)
            ? RandomNumberGenerator.GetBytes(CustomerAuthOptions.MinKeyBytes)
            : Convert.FromBase64String(options.Value.CsrfKey);
    }

    public string Create(long sessionId) =>
        WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture))));

    public bool IsValid(long sessionId, string? presented)
    {
        if (string.IsNullOrEmpty(presented))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Create(sessionId)), Encoding.UTF8.GetBytes(presented));
    }
}
