using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Romp.BuildingBlocks.Web;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Per-client fixed-window limits (ADR 0011): a generous default for every request and a stricter named policy for
    /// expensive endpoints. Limits come from configuration (<c>RateLimit:PerMinute</c>, <c>RateLimit:StrictPerMinute</c>).
    /// </summary>
    public static IServiceCollection AddRompRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var perMinute = configuration.GetValue("RateLimit:PerMinute", 1200);
        var strictPerMinute = configuration.GetValue("RateLimit:StrictPerMinute", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => Window(perMinute)));
            options.AddPolicy(RateLimitPolicies.Strict, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => Window(strictPerMinute)));
        });
        return services;
    }

    private static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
}
