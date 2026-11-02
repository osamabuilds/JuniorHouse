using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// For routes that have no anti-forgery token (cookie-authenticated guest or not-yet-signed-in writes): when the browser says where the
/// request came from, that origin must be this site's. A request with no <c>Origin</c> header is not a cross-site browser request, so it is
/// left to the other defences. The allowed origin is <c>Site:PublicBaseUrl</c>, else the module's own public URL
/// (<see cref="FallbackBaseUrl"/>), else the request's own origin. The browser talks to the storefront, not the API host, which is why the
/// request's own origin is the last resort. Modules derive a small class; nothing here knows about any module (ADR 0024).
/// </summary>
public abstract class OriginCheckFilterBase(IOptions<SiteOptions> site) : IEndpointFilter
{
    /// <summary>The module's own configured public address of the storefront, or null when it has none.</summary>
    protected virtual string? FallbackBaseUrl => null;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.HttpContext.Request;
        if (IsSafeMethod(request.Method) || !request.Headers.TryGetValue("Origin", out var origin) || origin.Count == 0)
        {
            return await next(context);
        }

        return IsAllowed(origin.ToString(), request)
            ? await next(context)
            : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
    }

    public static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private bool IsAllowed(string origin, HttpRequest request)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var presented)
            || presented.AbsolutePath != "/" || presented.Query.Length > 0 || presented.Fragment.Length > 0 || presented.UserInfo.Length > 0)
        {
            return false; // "null", junk, or something with a path in it
        }

        var baseUrl = string.IsNullOrWhiteSpace(site.Value.PublicBaseUrl) ? FallbackBaseUrl : site.Value.PublicBaseUrl;
        var allowed = !string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var configured)
            ? configured.GetLeftPart(UriPartial.Authority)
            : $"{request.Scheme}://{request.Host}";
        return string.Equals(presented.GetLeftPart(UriPartial.Authority), allowed, StringComparison.OrdinalIgnoreCase);
    }
}
