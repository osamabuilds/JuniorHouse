using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

// ADR 0008: VNDR consumes PROD's events through the shared dispatcher and applies the matching forward-only PO
// transition. Idempotent through VNDR's own inbox (INBX): the inbox row commits in the same SaveChanges as the transition.

/// <summary>Bulk cutting was recorded: Acknowledged -&gt; In Production (D1).</summary>
internal sealed class ProductionStartedConsumer(ISender sender, VendorDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<ProductionStartedEvent>
{
    public async Task HandleAsync(ProductionStartedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "StartProductionOnProductionStarted", cancellationToken))
        {
            await sender.Send(new ApplyProductionProgressCommand(domainEvent.PoId, ProductionProgress.Started), cancellationToken);
        }
    }
}

/// <summary>A delivery note was recorded: Partially Delivered or Delivered, as PROD judged it (AC-24).</summary>
internal sealed class DeliveryNoteRecordedConsumer(ISender sender, VendorDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<DeliveryNoteRecordedEvent>
{
    public async Task HandleAsync(DeliveryNoteRecordedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ApplyDeliveryOnDeliveryNoteRecorded", cancellationToken))
        {
            var progress = domainEvent.PoOutcome == "Delivered" ? ProductionProgress.Delivered : ProductionProgress.PartiallyDelivered;
            await sender.Send(new ApplyProductionProgressCommand(domainEvent.PoId, progress), cancellationToken);
        }
    }
}
