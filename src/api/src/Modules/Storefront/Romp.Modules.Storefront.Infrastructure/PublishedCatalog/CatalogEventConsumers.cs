using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

// Storefront consumes Catalog and Warehouse events through the shared dispatcher (ADR 0004, ADR 0011, ADR 0018). The inbox row commits with
// the projection, so a redelivered message is a no-op; the projection itself is also idempotent by version (PCT-5).

internal sealed class ProductPublishedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<ProductPublishedEvent>
{
    public async Task HandleAsync(ProductPublishedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncProductOnPublished", cancellationToken))
        {
            await sender.Send(new SyncProductCommand(domainEvent.ProductId), cancellationToken);
        }
    }
}

internal sealed class ProductChangedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<ProductChangedEvent>
{
    public async Task HandleAsync(ProductChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncProductOnChanged", cancellationToken))
        {
            await sender.Send(new SyncProductCommand(domainEvent.ProductId), cancellationToken);
        }
    }
}

internal sealed class ProductUnpublishedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<ProductUnpublishedEvent>
{
    public async Task HandleAsync(ProductUnpublishedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncProductOnUnpublished", cancellationToken))
        {
            await sender.Send(new SyncProductCommand(domainEvent.ProductId), cancellationToken);
        }
    }
}

internal sealed class ProductArchivedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<ProductArchivedEvent>
{
    public async Task HandleAsync(ProductArchivedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncProductOnArchived", cancellationToken))
        {
            await sender.Send(new SyncProductCommand(domainEvent.ProductId), cancellationToken);
        }
    }
}

internal sealed class SizeChartChangedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<SizeChartChangedEvent>
{
    public async Task HandleAsync(SizeChartChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncSizeChartOnChanged", cancellationToken))
        {
            await sender.Send(new SyncSizeChartCommand(domainEvent.ChartId, domainEvent.CategoryId, domainEvent.GenderId), cancellationToken);
        }
    }
}

internal sealed class StockLevelChangedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<StockLevelChangedEvent>
{
    public async Task HandleAsync(StockLevelChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncStockOnLevelChanged", cancellationToken))
        {
            await sender.Send(new SyncStockCommand(domainEvent.VariantId), cancellationToken);
        }
    }
}

internal sealed class StockPutawayConfirmedConsumer(ISender sender, StfrDbContext db, InboxGuard guard) : IOutboxMessageHandler<StockPutawayConfirmedEvent>
{
    public async Task HandleAsync(StockPutawayConfirmedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(db, "SyncStockOnPutawayConfirmed", cancellationToken))
        {
            await sender.Send(new SyncStockCommand(domainEvent.VariantId), cancellationToken);
        }
    }
}
