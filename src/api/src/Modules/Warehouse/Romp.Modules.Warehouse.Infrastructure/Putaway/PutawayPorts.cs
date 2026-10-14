using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Putaway;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.Putaway;

/// <summary>Putaway's one need from Vendor, read through its contract: the style a PO is for (one style per PO, labelling D11).</summary>
public sealed class ContractsPoStyleLookup(IPurchaseOrderQueries purchaseOrders, TimeProvider timeProvider) : IPoStyleLookup
{
    public async Task<long?> FindStyleIdForPoAsync(long poId, CancellationToken cancellationToken)
    {
        // The style is identical in every revision, so the terms in force today name it. Pakistan time is a fixed +05:00.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).DateTime);
        var terms = await purchaseOrders.GetTermsAtAsync(poId, today, cancellationToken);
        return terms?.StyleId;
    }
}

/// <summary>
/// Row locks (<c>SELECT … FOR UPDATE</c>) so two staff confirming the same item queue up instead of both seeing the same pending
/// quantity (PUT-9). Runs inside the caller's transaction; every identifier is quoted.
/// </summary>
public sealed class PutawayRowLocks(WhseDbContext dbContext) : IPutawayRowLocks
{
    public async Task LockItemAsync(long itemId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database
            .SqlQueryRaw<int>("""SELECT 1 AS "Value" FROM "WHSE"."PUT_ITEM" WHERE "ID" = {0} FOR UPDATE;""", itemId)
            .ToListAsync(cancellationToken);
}

/// <summary>
/// Warehouse consumes QC's <c>QcBatchAccepted</c> through the shared dispatcher (ADR 0004). The inbox row commits with the items, so a
/// redelivered message is a no-op (PUT-2); a message the mapper refuses is never stored (the consumer throws, the dispatcher retries and
/// then dead-letters it with the reason).
/// </summary>
internal sealed partial class QcBatchAcceptedConsumer(
    ISender sender, WhseDbContext dbContext, InboxGuard guard, IOptions<WarehouseOptions> options, ILogger<QcBatchAcceptedConsumer> logger)
    : IOutboxMessageHandler<QcBatchAcceptedEvent>
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Refused QcBatchAccepted (sub-batch {SubBatchId}): {Reason}")]
    private partial void LogRefused(long subBatchId, string reason);

    public async Task HandleAsync(QcBatchAcceptedEvent domainEvent, CancellationToken cancellationToken)
    {
        var mapped = PutawayQcMapper.Map(domainEvent, options.Value.MaxLinesPerAcceptedBatch);
        if (!mapped.IsValid)
        {
            LogRefused(domainEvent.SubBatchId, mapped.Rejection ?? string.Empty);
            throw new InvalidOperationException($"QcBatchAccepted refused: {mapped.Rejection}");
        }

        if (await guard.TryBeginAsync(dbContext, "RecordPutawayOnQcBatchAccepted", cancellationToken))
        {
            await sender.Send(new RecordAcceptedBatchCommand(mapped.Batch!), cancellationToken);
        }
    }
}

/// <summary>
/// A second consumer of Labelling's variant event (registered after the ledger's, which records the variant first): link every putaway
/// item that was waiting for this variant's GTIN (PUT-2). Confirm also links lazily, so event order never strands an item.
/// </summary>
internal sealed class VariantGtinAssignedPutawayConsumer(ISender sender, WhseDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<VariantGtinAssignedEvent>
{
    public async Task HandleAsync(VariantGtinAssignedEvent domainEvent, CancellationToken cancellationToken)
    {
        var (command, _) = VariantEventMapper.Map(domainEvent);
        if (command is null)
        {
            return; // the ledger's consumer already refused and reported it
        }

        if (await guard.TryBeginAsync(dbContext, "LinkPutawayItemsOnGtinAssigned", cancellationToken))
        {
            await sender.Send(new LinkVariantToItemsCommand(command.StyleId, command.ColourId, command.SizeId), cancellationToken);
        }
    }
}
