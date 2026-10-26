using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary><c>Identity:Csrf:*</c> - the server secret behind the anti-forgery values (sessions AC-10).</summary>
public sealed class CsrfOptions
{
    public const string Section = "Identity:Csrf";

    public const int MinKeyBytes = 32;

    /// <summary>Base64 of at least 32 random bytes; a deployment secret, never committed. Optional in Development/Testing only.</summary>
    public string? Key { get; set; }
}

public sealed class CsrfOptionsValidator(IHostEnvironment environment) : IValidateOptions<CsrfOptions>
{
    public ValidateOptionsResult Validate(string? name, CsrfOptions options)
    {
        if (string.IsNullOrEmpty(options.Key))
        {
            return RompAuthorizationExtensions.IsDevelopmentLike(environment)
                ? ValidateOptionsResult.Success // the process makes up its own key; restarting signs the browser's tokens out of date
                : ValidateOptionsResult.Fail($"{CsrfOptions.Section}:Key is required outside Development and Testing.");
        }

        var buffer = new byte[options.Key.Length];
        if (!Convert.TryFromBase64String(options.Key, buffer, out var written))
        {
            return ValidateOptionsResult.Fail($"{CsrfOptions.Section}:Key must be base64.");
        }

        return written < CsrfOptions.MinKeyBytes
            ? ValidateOptionsResult.Fail($"{CsrfOptions.Section}:Key must hold at least {CsrfOptions.MinKeyBytes} bytes.")
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// The per-session anti-forgery value: <c>HMAC-SHA256(serverKey, sessionId)</c>, base64url. A page script reads it from
/// <c>GET /api/auth/me</c> and sends it in <see cref="CsrfFilter.HeaderName"/>; a site that merely makes the browser send the cookie
/// cannot read it, so it cannot produce it. Nothing is stored: it is recomputed on each unsafe request.
/// </summary>
public sealed class CsrfTokenService
{
    private readonly byte[] _key;

    public CsrfTokenService(IOptions<CsrfOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _key = string.IsNullOrEmpty(options.Value.Key)
            ? RandomNumberGenerator.GetBytes(CsrfOptions.MinKeyBytes)
            : Convert.FromBase64String(options.Value.Key);
    }

    public string Create(long sessionId) =>
        WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture))));

    public bool IsValid(long sessionId, string? presented)
    {
        if (string.IsNullOrEmpty(presented))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Create(sessionId));
        var actual = Encoding.UTF8.GetBytes(presented);
        return CryptographicOperations.FixedTimeEquals(expected, actual); // false at once for different lengths; the length is not secret
    }
}

/// <summary>
/// Refuses an unsafe request (anything but GET, HEAD, OPTIONS, TRACE) from a signed-in caller unless it carries the right anti-forgery
/// value (AC-10). A request with no session is not checked here: sign-in has none yet and is guarded by <see cref="OriginCheckFilter"/>,
/// and every other route refuses an anonymous caller anyway. The refusal never says what was wrong.
/// </summary>
public sealed class CsrfFilter(CsrfTokenService tokens) : IEndpointFilter
{
    public const string HeaderName = "X-Romp-Csrf";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        if (IsSafe(http.Request.Method) || SessionClaims.Session(http.User) is not { } sessionId)
        {
            return await next(context);
        }

        return tokens.IsValid(sessionId, http.Request.Headers[HeaderName].ToString())
            ? await next(context)
            : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
    }

    internal static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}

/// <summary>
/// For sign-in, which has no session to hold an anti-forgery value: when the browser says where the request came from, that origin must
/// be this site's (<c>Site:PublicBaseUrl</c>, or the request's own origin when none is configured). A request with no <c>Origin</c>
/// header is not a cross-site browser request (browsers always send one on those), so it is left to the other defences.
/// </summary>
public sealed class OriginCheckFilter(IOptions<SiteOptions> site) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.HttpContext.Request;
        if (CsrfFilter.IsSafe(request.Method) || !request.Headers.TryGetValue("Origin", out var origin) || origin.Count == 0)
        {
            return await next(context);
        }

        return IsAllowed(origin.ToString(), request)
            ? await next(context)
            : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
    }

    private bool IsAllowed(string origin, HttpRequest request)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var presented)
            || presented.AbsolutePath != "/" || presented.Query.Length > 0 || presented.Fragment.Length > 0 || presented.UserInfo.Length > 0)
        {
            return false; // "null", junk, or something with a path in it
        }

        var allowed = !string.IsNullOrWhiteSpace(site.Value.PublicBaseUrl) && Uri.TryCreate(site.Value.PublicBaseUrl, UriKind.Absolute, out var configured)
            ? configured.GetLeftPart(UriPartial.Authority)
            : $"{request.Scheme}://{request.Host}";
        return string.Equals(presented.GetLeftPart(UriPartial.Authority), allowed, StringComparison.OrdinalIgnoreCase)
            && string.Equals(presented.Scheme, new Uri(allowed).Scheme, StringComparison.OrdinalIgnoreCase);
    }
}

public static class CsrfServiceCollectionExtensions
{
    /// <summary>Binds <c>Identity:Csrf</c>, validates it at start-up and registers the token service the filters use.</summary>
    public static IServiceCollection AddSessionCsrf(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CsrfOptions>().Bind(configuration.GetSection(CsrfOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CsrfOptions>, CsrfOptionsValidator>();
        services.AddSingleton<CsrfTokenService>();
        return services;
    }
}
