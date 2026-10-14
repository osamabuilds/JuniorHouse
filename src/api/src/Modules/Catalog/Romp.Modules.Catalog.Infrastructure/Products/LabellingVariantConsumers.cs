using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Labelling.Contracts;

namespace Romp.Modules.Catalog.Infrastructure.Products;

/// <summary>
/// Catalog consumes Labelling's <c>VariantGtinAssigned</c> through the shared dispatcher (ADR 0004, ADR 0014). The inbox row commits
/// with the upsert, so a redelivered message is a no-op; the upsert itself is idempotent and order-independent (PRD-15).
/// </summary>
internal sealed class VariantGtinAssignedConsumer(ISender sender, CatalogDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<VariantGtinAssignedEvent>
{
    public async Task HandleAsync(VariantGtinAssignedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ApplyVariantOnGtinAssigned", cancellationToken))
        {
            await sender.Send(
                new ApplyLabellingVariantCommand(new LabelledVariantInfo(
                    domainEvent.VariantId, domainEvent.StyleId, domainEvent.ColourId, domainEvent.SizeId, domainEvent.SkuCode, domainEvent.Gtin, false)),
                cancellationToken);
        }
    }
}

/// <summary>Catalog consumes Labelling's <c>GtinVoided</c> the same way: the variant stops being sellable (PRD-14).</summary>
internal sealed class GtinVoidedConsumer(ISender sender, CatalogDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<GtinVoidedEvent>
{
    public async Task HandleAsync(GtinVoidedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ApplyVariantOnGtinVoided", cancellationToken))
        {
            await sender.Send(
                new ApplyLabellingVariantCommand(new LabelledVariantInfo(
                    domainEvent.VariantId, domainEvent.StyleId, domainEvent.ColourId, domainEvent.SizeId, domainEvent.SkuCode, domainEvent.Gtin, true)),
                cancellationToken);
        }
    }
}
