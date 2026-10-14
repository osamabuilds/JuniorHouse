using Microsoft.AspNetCore.Builder;

namespace Romp.BuildingBlocks.Web;

public static class SecurityHeadersExtensions
{
    /// <summary>Adds the baseline response headers of ADR 0011 to every response.</summary>
    public static IApplicationBuilder UseRompSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            await next();
        });
}
