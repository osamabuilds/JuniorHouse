using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Vendors;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

// Sprint 4 (ADR 0004, ADR 0008): VNDR consumes QC's and PROD's events through the shared dispatcher. Each consumer is
// idempotent: the inbox row (keyed by the outbox message id and this handler) commits in the same SaveChanges as the effect,
// so a redelivered message is a no-op (AC-38, AC-41).

/// <summary>A goods receipt was recorded (QC): note the receipt progress on the PO. It changes no PO status (Q7 default, AC-40, AC-41).</summary>
internal sealed class GoodsReceiptRecordedConsumer(ISender sender, VendorDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<GoodsReceiptRecordedEvent>
{
    public async Task HandleAsync(GoodsReceiptRecordedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ApplyReceiptOnGoodsReceiptRecorded", cancellationToken))
        {
            await sender.Send(
                new ApplyGoodsReceiptRecordedCommand(
                    domainEvent.PoId, domainEvent.GrnId, domainEvent.GrnNo, domainEvent.ReceivedDate,
                    domainEvent.Lines.Select(l => new ReceiptLineInput(l.SizeId, l.ColourId, l.ClaimedQty, l.CountedQty)).ToList()),
                cancellationToken);
        }
    }
}

/// <summary>A delivery note was recorded (PROD): remember its on-time and on-quantity facts for the vendor scorecard (AC-35, AC-36, AC-38).</summary>
internal sealed class ScorecardDeliveryConsumer(ISender sender, VendorDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<DeliveryNoteRecordedEvent>
{
    public async Task HandleAsync(DeliveryNoteRecordedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "RecordScorecardDeliveryOnDeliveryNote", cancellationToken))
        {
            await sender.Send(
                new RecordScorecardDeliveryCommand(
                    domainEvent.PoId, domainEvent.VendorDnNo, domainEvent.DispatchDate, domainEvent.TimingCode, domainEvent.QuantityCode,
                    domainEvent.RompFabricDelay),
                cancellationToken);
        }
    }
}

/// <summary>An inspection was recorded (QC): remember a batch's first pass for the vendor's defect rate (AC-35, AC-38).</summary>
internal sealed class ScorecardInspectionConsumer(ISender sender, VendorDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<QcInspectionRecordedEvent>
{
    public async Task HandleAsync(QcInspectionRecordedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "RecordScorecardInspectionOnInspectionRecorded", cancellationToken))
        {
            await sender.Send(
                new RecordScorecardInspectionCommand(
                    domainEvent.PoId, domainEvent.BatchId, domainEvent.SubBatchId, domainEvent.RoundNo, domainEvent.SampleQty,
                    domainEvent.DefectiveQty, domainEvent.OccurredAt),
                cancellationToken);
        }
    }
}
