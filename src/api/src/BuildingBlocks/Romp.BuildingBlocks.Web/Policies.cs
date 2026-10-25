namespace Romp.BuildingBlocks.Web;

/// <summary>Authorization policy names (ADR 0011). Every endpoint group declares one through <see cref="SecuredEndpointExtensions.MapSecuredGroup"/>.</summary>
public static class Policies
{
    /// <summary>Any signed-in back-office user.</summary>
    public const string Staff = "Staff";

    /// <summary>Administrative actions (void, cancel, operational data). Staff-equivalent until roles exist (S8, SEC-08).</summary>
    public const string Admin = "Admin";

    /// <summary>Stock actions (adjustments, counts, alert thresholds; FR-54, FR-60). Capability-shaped: Admin-equivalent until roles exist (S8, SEC-08).</summary>
    public const string Inventory = "Inventory";

    /// <summary>Catalogue actions (products, images, size charts, URLs, publish; Sprint 7). Capability-shaped: Admin-equivalent until roles exist (S8, SEC-08).</summary>
    public const string Catalogue = "Catalogue";

    /// <summary>Storefront settings and content pages (Sprint 9, ADR 0020). Capability-shaped: Admin-equivalent until roles exist (S8, SEC-08).</summary>
    public const string StorefrontSettings = "StorefrontSettings";

    /// <summary>Search synonyms and the zero-result log (Sprint 9, search SRC-12, 13). Capability-shaped: Admin-equivalent until roles exist (S8, SEC-08).</summary>
    public const string StorefrontSearch = "StorefrontSearch";

    /// <summary>Deliberately open to anonymous callers: the public storefront reads (published settings and pages). Naming it keeps ADR 0011 true: every group states who may call it.</summary>
    public const string Public = "Public";
}

/// <summary>Named rate-limit policies (ADR 0011).</summary>
public static class RateLimitPolicies
{
    /// <summary>For expensive or abuse-prone endpoints (preview, queue creation, resolve).</summary>
    public const string Strict = "strict";
}
