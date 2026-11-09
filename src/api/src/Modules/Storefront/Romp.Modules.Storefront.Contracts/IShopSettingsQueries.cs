namespace Romp.Modules.Storefront.Contracts;

/// <summary>The shop details a packing slip prints, from the published settings and not from text in a template (SLP-4).</summary>
public sealed record ShopSlipSettings(string BrandName, string? Whatsapp, string? Email, string? Hours, int ExchangeWindowDays);

/// <summary>What other modules may ask Storefront about the shop's published details (ADR 0002: through Contracts only).</summary>
public interface IShopSettingsQueries
{
    /// <summary>The current published details, or null before the first publish.</summary>
    Task<ShopSlipSettings?> GetAsync(CancellationToken cancellationToken);
}
