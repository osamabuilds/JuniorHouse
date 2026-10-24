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
