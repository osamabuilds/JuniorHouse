namespace Romp.Modules.Catalog.Application.Abstractions;

/// <summary>
/// Configuration, not constants (module plan "Settings"). Bound from <c>Catalog:*</c> and validated at start-up by the module: a bad
/// value stops the host from starting.
/// </summary>
public sealed class CatalogOptions
{
    /// <summary>The largest image accepted (Q-MED-2). Default 5 MiB.</summary>
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>The most images one product can hold (Q-MED-2). Default 10.</summary>
    public int MaxImagesPerProduct { get; set; } = 10;

    /// <summary>The image types accepted, by content: <c>jpeg</c>, <c>png</c>, <c>webp</c>.</summary>
    public string[] AllowedImageTypes { get; set; } = ["jpeg", "png", "webp"];

    /// <summary>The longest slug accepted (URL-8).</summary>
    public int MaxSlugLength { get; set; } = 80;

    /// <summary>Slugs nobody may take, because they would shadow a site route (URL-7).</summary>
    public string[] ReservedSlugs { get; set; } =
    [
        "cart", "checkout", "account", "admin", "search", "login", "logout", "register", "api", "sitemap", "robots", "orders", "wishlist",
        "collections", "products", "static", "assets", "health",
    ];
}
