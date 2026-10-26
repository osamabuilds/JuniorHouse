using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

public static class AuthRoutes
{
    public const string Prefix = "/api/auth";

    /// <summary>
    /// The group every sign-in, refresh, sign-out and account-security route is mapped into. All of them share one strict per-address
    /// limit on top of the sign-in throttle (sessions AC-12), so a flood of guesses, refreshes or code attempts from one address
    /// is cut off before it reaches the database. The group itself is public (sign-in has no session); routes that need one add
    /// <c>RequireAuthorization()</c> themselves.
    /// </summary>
    public static RouteGroupBuilder MapAuthRoutes(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapSecuredGroup(Prefix, Policies.Public).RequireRateLimiting(RateLimitPolicies.Strict);
    }
}
