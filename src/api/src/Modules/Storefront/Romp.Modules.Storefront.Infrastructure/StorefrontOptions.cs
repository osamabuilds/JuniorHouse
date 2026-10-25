using Microsoft.Extensions.Options;

namespace Romp.Modules.Storefront.Infrastructure;

/// <summary>Bound from <c>Storefront:*</c>. Configuration, not constants. Shared by both Storefront features, so it lives at the module root.</summary>
public sealed class StorefrontOptions
{
    /// <summary>The external sites a navigation or footer link may point at (plan: allow-listed). Hosts only, no scheme.</summary>
    public string[] AllowedExternalHosts { get; set; } = ["wa.me"];

    /// <summary>
    /// Whether the admin routes for settings and pages are mapped. On by default; outside Development/Testing the host will not start with it on
    /// unless real authentication is registered (<see cref="StorefrontSettings.StorefrontAdminRoutesGuard"/>).
    /// </summary>
    public bool EnableAdminRoutes { get; set; } = true;

    /// <summary>At or below this many units a variant reads "only a few left" (published-catalog PCT-3).</summary>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>The widths each product image is resized to, in WebP and JPEG (published-catalog PCT-7).</summary>
    public int[] ImageWidths { get; set; } = [320, 640, 960, 1280];

    /// <summary>Largest original, in pixels (width × height), the image worker will decode.</summary>
    public long MaxImagePixels { get; set; } = 40_000_000;

    /// <summary>Encoder quality (1 to 100) of every derivative.</summary>
    public int ImageQuality { get; set; } = 80;

    /// <summary>Attempts a derivative gets before it is marked failed.</summary>
    public int MaxDerivativeAttempts { get; set; } = 5;

    /// <summary>Wait after the first failed attempt; doubled for each further one.</summary>
    public TimeSpan DerivativeRetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>How long an unreferenced derivative file is kept before the sweep removes it.</summary>
    public TimeSpan DerivativeCacheLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>How often the sweep looks for unreferenced derivative files.</summary>
    public TimeSpan DerivativeSweepInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Whether the background image worker runs. On by default.</summary>
    public bool ImageWorkerEnabled { get; set; } = true;

    /// <summary>How long the image worker sleeps when no derivative is waiting.</summary>
    public TimeSpan ImageWorkerPollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Whether the background reconcile job runs (published-catalog PCT-5). On by default.</summary>
    public bool ReconcileEnabled { get; set; } = true;

    /// <summary>Gender lookup codes that are not header menu entries (owner, 2026-10-24: Unisex is not one). Stable lookup codes, case-insensitive.</summary>
    public string[] MenuHiddenGenderCodes { get; set; } = ["UNISEX"];

    /// <summary>New In shows products published within this many days (listings LST-7, Q-LST-3 default 30).</summary>
    public int NewArrivalsDays { get; set; } = 30;

    /// <summary>Most products one search considers before filters and paging (search plan, Settings).</summary>
    public int SearchMaxHits { get; set; } = 200;

    /// <summary>Below this many full-text matches a search also looks for near spellings.</summary>
    public int SearchMinFullTextHits { get; set; } = 3;

    /// <summary>Minimum trigram word-similarity (0 to 1) for a near spelling to count.</summary>
    public double SearchSimilarityThreshold { get; set; } = 0.45;

    /// <summary>Most items autosuggest returns.</summary>
    public int SuggestLimit { get; set; } = 8;

    /// <summary>How often the reconcile job compares the read model with Catalog and Warehouse.</summary>
    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromMinutes(10);
}

public sealed class ReadModelSettings(IOptions<StorefrontOptions> options) : Application.PublishedCatalog.IReadModelSettings
{
    public int LowStockThreshold => options.Value.LowStockThreshold;

    public IReadOnlyList<int> ImageWidths => options.Value.ImageWidths;

    public long MaxImagePixels => options.Value.MaxImagePixels;

    public int ImageQuality => options.Value.ImageQuality;

    public int MaxDerivativeAttempts => options.Value.MaxDerivativeAttempts;

    public TimeSpan DerivativeRetryDelay => options.Value.DerivativeRetryDelay;

    public TimeSpan DerivativeCacheLifetime => options.Value.DerivativeCacheLifetime;
}
