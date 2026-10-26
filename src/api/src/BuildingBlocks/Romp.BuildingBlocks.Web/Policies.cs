namespace Romp.BuildingBlocks.Web;

/// <summary>
/// Authorization policy names (ADR 0011). The role-shaped policies of Sprints 1 to 7 are gone: every secured group names a permission
/// (<see cref="Permissions"/>), decided per request from the database (roles spec). Only the explicit "open to everyone" name remains.
/// </summary>
public static class Policies
{
    /// <summary>Deliberately open to anonymous callers: the public storefront reads (published settings and pages). Naming it keeps ADR 0011 true: every group states who may call it.</summary>
    public const string Public = "Public";

    /// <summary>A signed-in shopper (Customers module). Until real customer sign-in exists (the final authentication sprint) only a development-only stand-in satisfies it, in Development and Testing (ADR 0021).</summary>
    public const string Customer = "Customer";
}

/// <summary>Named rate-limit policies (ADR 0011).</summary>
public static class RateLimitPolicies
{
    /// <summary>For expensive or abuse-prone endpoints (preview, queue creation, resolve).</summary>
    public const string Strict = "strict";
}
