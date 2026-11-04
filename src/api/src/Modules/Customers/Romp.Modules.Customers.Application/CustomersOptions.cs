namespace Romp.Modules.Customers.Application;

/// <summary>The <c>Customers</c> configuration section (module plan, "Policies, configuration and the web edge"). Validated at start-up.</summary>
public sealed class CustomersOptions
{
    public const string SectionName = "Customers";

    /// <summary>How many saved addresses one customer may keep (ADD-6, Q-ADD-3 default).</summary>
    public int MaxAddresses { get; set; } = 10;

    /// <summary>How many products one customer may keep on the wishlist (plan: 200).</summary>
    public int MaxWishlistItems { get; set; } = 200;

    /// <summary>The most open back-in-stock requests one variant keeps (Notify Me plan: 5,000).</summary>
    public int MaxNotifyPerVariant { get; set; } = 5000;

    /// <summary>Days an open back-in-stock request waits before it expires (Q-NTM-2).</summary>
    public int NotifyOpenDays { get; set; } = 90;

    /// <summary>Days a closed (notified or expired) back-in-stock request is kept before it is deleted (Q-NTM-2).</summary>
    public int NotifyClosedDays { get; set; } = 30;

    /// <summary>The storefront address that emailed links point at (verify email, reset password). Configuration only, never taken from a request.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:4000";

    /// <summary>The development key. It only works in Development and Testing; the module refuses to start with it anywhere else.</summary>
    public const string DevelopmentLinkSigningKey = "development-only-link-signing-key-do-not-use";

    /// <summary>The secret that signs links in email (unsubscribe, NLS-3). From secrets or configuration, never committed.</summary>
    public string LinkSigningKey { get; set; } = DevelopmentLinkSigningKey;

    /// <summary>True when the signing key is a real secret: long enough and not the development key.</summary>
    public static bool HasProductionKey(CustomersOptions options) =>
        options.LinkSigningKey.Length >= 32 && options.LinkSigningKey != DevelopmentLinkSigningKey;

    public static bool IsValid(CustomersOptions options) =>
        options.MaxAddresses is >= 1 and <= 50
        && options.MaxWishlistItems is >= 1 and <= 1000
        && options.MaxNotifyPerVariant is >= 1 and <= 100000
        && options.NotifyOpenDays is >= 1 and <= 365
        && options.NotifyClosedDays is >= 1 and <= 365
        && options.LinkSigningKey.Length >= 16
        && Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var url)
        && url.Scheme is "http" or "https";
}
