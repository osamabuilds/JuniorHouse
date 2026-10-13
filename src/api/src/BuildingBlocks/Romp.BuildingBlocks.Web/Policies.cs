namespace Romp.BuildingBlocks.Web;

/// <summary>Authorization policy names (ADR 0011). Every endpoint group declares one through <see cref="SecuredEndpointExtensions.MapSecuredGroup"/>.</summary>
public static class Policies
{
    /// <summary>Any signed-in back-office user.</summary>
    public const string Staff = "Staff";

    /// <summary>Administrative actions (void, cancel, operational data). Staff-equivalent until roles exist (S8, SEC-08).</summary>
    public const string Admin = "Admin";
}

/// <summary>Named rate-limit policies (ADR 0011).</summary>
public static class RateLimitPolicies
{
    /// <summary>For expensive or abuse-prone endpoints (preview, queue creation, resolve).</summary>
    public const string Strict = "strict";
}
