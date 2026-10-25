using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Contracts;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

/// <summary>Bound from <c>Storefront:*</c>. Configuration, not constants.</summary>
public sealed class StorefrontOptions
{
    /// <summary>The external sites a navigation or footer link may point at (plan: allow-listed). Hosts only, no scheme.</summary>
    public string[] AllowedExternalHosts { get; set; } = ["wa.me"];

    /// <summary>
    /// Whether the admin routes for settings and pages are mapped. On by default; outside Development/Testing the host will not start with it on
    /// unless real authentication is registered (<see cref="StorefrontAdminRoutesGuard"/>).
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

public sealed class ExternalHostPolicy(IOptions<StorefrontOptions> options) : IExternalHostPolicy
{
    public IReadOnlyCollection<string> AllowedHosts => options.Value.AllowedExternalHosts;
}

/// <summary>
/// Storefront's unit of work: runs the handler inside one database transaction, then saves, so the change and its outbox event commit
/// together (ADR 0004, NFR-FT-08). A command sent from inside another command's handler joins the outer transaction.
/// </summary>
public sealed class StorefrontTransactionBehavior<TRequest, TResponse>(StfrDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IStorefrontCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return nested;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var response = await next(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}

/// <summary>The 60-second in-memory copy of the current document (ST-13). One per process; a new snapshot drops it.</summary>
public sealed class PublishedDocumentCache(IMemoryCache cache) : IPublishedDocumentCache
{
    private const string Key = "storefront.published-document";
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public async Task<PublishedDocument?> GetOrLoadAsync(Func<CancellationToken, Task<PublishedDocument?>> load, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key, out PublishedDocument? cached))
        {
            return cached;
        }

        var document = await load(cancellationToken);
        if (document is not null)
        {
            // Nothing published yet is not cached, so the first publish is visible at once.
            cache.Set(Key, document, Lifetime);
        }

        return document;
    }

    public void Invalidate() => cache.Remove(Key);
}

/// <summary>Drops the cached document once a new snapshot is durably committed (idempotent, so a redelivery is harmless).</summary>
public sealed class SettingsPublishedCacheInvalidator(IPublishedDocumentCache cache) : IOutboxMessageHandler<StorefrontSettingsPublishedEvent>
{
    public Task HandleAsync(StorefrontSettingsPublishedEvent domainEvent, CancellationToken cancellationToken)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }
}
