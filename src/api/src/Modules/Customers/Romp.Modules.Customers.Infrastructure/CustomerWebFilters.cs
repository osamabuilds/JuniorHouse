using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Infrastructure.Accounts;

namespace Romp.Modules.Customers.Infrastructure;

/// <summary>
/// Refuses an unsafe request from a signed-in customer unless it carries the right anti-forgery value (SameSite=Lax alone does not stop a
/// cross-site POST that a top-level form can make). The refusal never says what was wrong.
/// </summary>
public sealed class CustomerCsrfFilter(CustomerCsrfTokens tokens) : IEndpointFilter
{
    public const string HeaderName = "X-Romp-Csrf";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        if (IsSafe(http.Request.Method) || CustomerSessionPrincipal.SessionId(http.User) is not { } sessionId)
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
/// For the routes that have no session yet (register, sign-in, reset): the browser's <c>Origin</c> must be the site's (the rule lives in
/// <see cref="OriginCheckFilterBase"/>). The customer module's fallback is the storefront address its mail links use.
/// </summary>
public sealed class CustomerOriginCheckFilter(IOptions<SiteOptions> site, IOptions<CustomersOptions> customers) : OriginCheckFilterBase(site)
{
    protected override string? FallbackBaseUrl => customers.Value.PublicBaseUrl;
}
