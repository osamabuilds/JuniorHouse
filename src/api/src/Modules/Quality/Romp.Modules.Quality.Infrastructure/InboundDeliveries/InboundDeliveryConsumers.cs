using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Quality.Application.InboundDeliveries;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Quality.Infrastructure.InboundDeliveries;

// QC consumes PROD's and VNDR's events through the shared dispatcher (ADR 0004, ADR 0008). Each consumer is idempotent:
// the inbox row (keyed by the outbox message id and this handler) is added to QC's own DbContext and commits in the same
// SaveChanges as the effect, so a redelivered message is a no-op (AC-2) and a failed one leaves nothing behind.

/// <summary>A vendor delivery note was recorded (PROD): QC stores its own copy for the receiving queue (AC-1).</summary>
internal sealed class DeliveryNoteRecordedConsumer(ISender sender, QcDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<DeliveryNoteRecordedEvent>
{
    public async Task HandleAsync(DeliveryNoteRecordedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "RecordInboundDeliveryOnDeliveryNote", cancellationToken))
        {
            await sender.Send(
                new RecordInboundDeliveryCommand(
                    domainEvent.RunId, domainEvent.PoId, domainEvent.PoNo, domainEvent.VendorDnNo, domainEvent.DispatchDate,
                    domainEvent.IsFinalShipment,
                    domainEvent.Lines.Select(l => new ClaimedLine(l.SizeId, l.ColourId, l.Qty)).ToList()),
                cancellationToken);
        }
    }
}

/// <summary>The PO was cancelled (VNDR): its open inbound deliveries cannot be received any more (AC-5).</summary>
internal sealed class PoCancelledInboundConsumer(ISender sender, QcDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<PoCancelledEvent>
{
    public async Task HandleAsync(PoCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CancelInboundOnPoCancelled", cancellationToken))
        {
            await sender.Send(new MarkInboundDeliveriesCancelledCommand(domainEvent.PoId), cancellationToken);
        }
    }
}
