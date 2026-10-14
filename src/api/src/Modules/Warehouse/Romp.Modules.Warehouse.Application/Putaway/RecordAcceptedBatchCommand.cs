using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain.Putaway;

namespace Romp.Modules.Warehouse.Application.Putaway;

public sealed record MappedPutawayLine(short SizeId, short ColourId, int Qty);

public sealed record MappedPutawayBatch(long SubBatchId, long QcBatchId, long PoId, short GradeId, IReadOnlyList<MappedPutawayLine> Lines);

/// <summary>The result of mapping QC's event: a clean batch, or the reason it was refused.</summary>
public sealed record PutawayMappingResult(MappedPutawayBatch? Batch, string? Rejection)
{
    public bool IsValid => Batch is not null;
}

/// <summary>
/// The anti-corruption layer (ADR 0011): the ONLY file in Warehouse that knows the shape of QC's <see cref="QcBatchAcceptedEvent"/>.
/// It validates before anything is stored, so a forged or malformed message cannot create putaway items, and it accepts only the two
/// released outcomes: rejected, rework and undecided quantity never reaches the shelf (PUT-12, FR-SC-05).
/// </summary>
public static class PutawayQcMapper
{
    public static PutawayMappingResult Map(QcBatchAcceptedEvent message, int maxLines)
    {
        if (message.SubBatchId <= 0 || message.BatchId <= 0 || message.PoId <= 0)
        {
            return Reject("The batch, sub-batch and PO ids must be positive.");
        }

        if (PutawayItem.GradeFromOutcome(message.OutcomeCode) is not { } grade)
        {
            return Reject($"Outcome '{message.OutcomeCode}' is not a released outcome; only Accepted and Concession go to the shelf.");
        }

        if (message.Lines is null || message.Lines.Count == 0)
        {
            return Reject("The message has no lines.");
        }

        if (message.Lines.Count > maxLines)
        {
            return Reject($"The message has {message.Lines.Count} lines; the limit is {maxLines}.");
        }

        var seen = new HashSet<(short, short)>();
        var lines = new List<MappedPutawayLine>(message.Lines.Count);
        foreach (var line in message.Lines)
        {
            if (line.Qty <= 0 || line.SizeId <= 0 || line.ColourId <= 0)
            {
                return Reject("Every line needs a positive quantity, size and colour.");
            }

            if (!seen.Add((line.SizeId, line.ColourId)))
            {
                return Reject($"Size {line.SizeId} and colour {line.ColourId} appear twice in one sub-batch.");
            }

            lines.Add(new MappedPutawayLine(line.SizeId, line.ColourId, line.Qty));
        }

        return new PutawayMappingResult(new MappedPutawayBatch(message.SubBatchId, message.BatchId, message.PoId, grade, lines), null);
    }

    private static PutawayMappingResult Reject(string reason) => new(null, reason);
}

/// <summary>
/// Records the released lines of one accepted sub-batch as putaway items (PUT-1). A line already recorded (a redelivered message) is
/// skipped, so the pending quantity never doubles (PUT-2). Each item is linked to its variant when Labelling has already assigned a
/// GTIN, and waits as AwaitingGtin otherwise (the event can arrive in either order).
/// </summary>
public sealed record RecordAcceptedBatchCommand(MappedPutawayBatch Batch) : IRequest<int>, IWarehouseCommand;

public sealed class RecordAcceptedBatchCommandHandler(IWarehouseDbContext dbContext, IPoStyleLookup styles, IStockVariantReader variants)
    : IRequestHandler<RecordAcceptedBatchCommand, int>
{
    public async Task<int> Handle(RecordAcceptedBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = request.Batch;
        var styleId = await styles.FindStyleIdForPoAsync(batch.PoId, cancellationToken)
            ?? throw new InvalidOperationException($"PO {batch.PoId} has no style yet, so its accepted stock cannot be put away.");

        var existing = await dbContext.PutawayItems.AsNoTracking()
            .Where(i => i.SubBatchId == batch.SubBatchId)
            .Select(i => new { i.SizeId, i.ColourId })
            .ToListAsync(cancellationToken);
        var known = existing.Select(e => (e.SizeId, e.ColourId)).ToHashSet();

        var created = 0;
        foreach (var line in batch.Lines.Where(l => !known.Contains((l.SizeId, l.ColourId))))
        {
            var variant = await variants.FindByStyleColourSizeAsync(styleId, line.ColourId, line.SizeId, cancellationToken);
            dbContext.PutawayItems.Add(new PutawayItem(
                batch.SubBatchId, batch.QcBatchId, batch.PoId, styleId, line.ColourId, line.SizeId, batch.GradeId, line.Qty, variant?.Id));
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }
}

/// <summary>
/// Labelling assigned a GTIN to a variant: link every item waiting for it (PUT-2, event-order safety). Items that were already
/// linked are untouched.
/// </summary>
public sealed record LinkVariantToItemsCommand(long StyleId, short ColourId, short SizeId) : IRequest<int>, IWarehouseCommand;

public sealed class LinkVariantToItemsCommandHandler(IWarehouseDbContext dbContext, IStockVariantReader variants)
    : IRequestHandler<LinkVariantToItemsCommand, int>
{
    public async Task<int> Handle(LinkVariantToItemsCommand request, CancellationToken cancellationToken)
    {
        var variant = await variants.FindByStyleColourSizeAsync(request.StyleId, request.ColourId, request.SizeId, cancellationToken);
        if (variant is null)
        {
            return 0;
        }

        var waiting = await dbContext.PutawayItems
            .Where(i => i.StyleId == request.StyleId && i.ColourId == request.ColourId && i.SizeId == request.SizeId && i.VariantRefId == null)
            .ToListAsync(cancellationToken);
        foreach (var item in waiting)
        {
            item.LinkVariant(variant.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return waiting.Count;
    }
}
