using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.Putaway;

/// <summary>
/// Shelves units of one putaway item into one bin (FR-SC-08, PUT-4): the GTIN is scanned (or, when no tag exists yet, the item is
/// chosen from the list, PUT-19) and the bin is scanned or picked. One transaction does it all: row lock on the item, the checks, the
/// ledger posting, the immutable record and the <c>StockPutawayConfirmedEvent</c> in the outbox. A repeat of the same idempotency key
/// returns the original record and changes nothing (PUT-8).
/// </summary>
public sealed record ConfirmPutawayCommand(
    long ItemId, string BinCode, int Quantity, bool Scanned, string? Gtin, Guid IdempotencyKey, string ActingPerson)
    : IRequest<PutawayRecordDto>, IWarehouseCommand;

public sealed class ConfirmPutawayCommandValidator : AbstractValidator<ConfirmPutawayCommand>
{
    public ConfirmPutawayCommandValidator()
    {
        RuleFor(c => c.ItemId).GreaterThan(0);
        RuleFor(c => c.BinCode).NotEmpty().MaximumLength(12);
        RuleFor(c => c.Quantity).InclusiveBetween(1, 100_000).WithMessage("The quantity must be a whole number from 1 to 100,000.");
        RuleFor(c => c.IdempotencyKey).NotEmpty();
        RuleFor(c => c.Gtin).Must(g => Gtin13.IsValid(g)).When(c => c.Scanned).WithMessage("The scanned barcode is not a valid GTIN-13.");
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class ConfirmPutawayCommandHandler(
    IWarehouseDbContext dbContext,
    IPutawayRowLocks locks,
    IBinReader bins,
    IStockVariantReader variants,
    IStockPosting posting) : IRequestHandler<ConfirmPutawayCommand, PutawayRecordDto>
{
    public async Task<PutawayRecordDto> Handle(ConfirmPutawayCommand request, CancellationToken cancellationToken)
    {
        var repeat = await dbContext.PutawayRecords.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (repeat is not null)
        {
            return await ToDtoAsync(repeat, cancellationToken);
        }

        await locks.LockItemAsync(request.ItemId, cancellationToken); // serialises concurrent confirmations of one item (PUT-9)

        // Re-check after the lock: a concurrent request with the same key may have just committed.
        repeat = await dbContext.PutawayRecords.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (repeat is not null)
        {
            return await ToDtoAsync(repeat, cancellationToken);
        }

        var item = await dbContext.PutawayItems.FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Putaway item {request.ItemId} was not found.");

        var variantId = item.VariantRefId
            ?? (await variants.FindByStyleColourSizeAsync(item.StyleId, item.ColourId, item.SizeId, cancellationToken))?.Id
            ?? throw new DomainException("This item is still waiting for its GTIN, so it cannot be put away yet.");
        item.LinkVariant(variantId);

        var variant = await variants.FindByIdAsync(variantId, cancellationToken)
            ?? throw new DomainException("This item's variant is not known to the warehouse yet.");
        if (variant.GtinRetired)
        {
            throw new DomainException("This barcode has been retired. Its stock cannot be put away until Labelling issues a new barcode.");
        }

        if (request.Scanned && !string.Equals(request.Gtin, variant.Gtin, StringComparison.Ordinal))
        {
            throw new DomainException("The scanned barcode does not belong to this item.");
        }

        var bin = await bins.FindByCodeAsync(request.BinCode, cancellationToken)
            ?? throw new DomainException($"Bin {request.BinCode.Trim().ToUpperInvariant()} does not exist.");
        if (!bin.IsActive)
        {
            throw new DomainException($"Bin {bin.BinCode} is inactive.");
        }

        item.Record(request.Quantity);

        var posted = await posting.PostAsync(
            [new StockPost(variantId, item.GradeId, bin.Id, request.Quantity, MovementKinds.Putaway, item.Id)],
            request.ActingPerson.Trim(),
            cancellationToken);

        var record = new PutawayRecord(item.Id, bin.Id, request.Quantity, request.Scanned, request.IdempotencyKey, request.ActingPerson.Trim());
        dbContext.PutawayRecords.Add(record);

        // StockPutawayConfirmedEvent (§9.10.3): this is the moment the quantity becomes available (FR-SC-08). Same transaction as the stock.
        var onHand = await variants.GetOnHandAsync(variantId, cancellationToken);
        var tracked = await dbContext.VariantRefs.FirstAsync(v => v.Id == variantId, cancellationToken);
        tracked.PublishPutaway(Grades.CodeOf(item.GradeId), request.Quantity, onHand, posted[0].StockVersion);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PutawayRecordDto(
            record.Id, item.Id, bin.Id, bin.BinCode, record.Quantity, record.Scanned, record.ActorName, record.InsrDte, item.Pending, variant.SkuCode);
    }

    private async Task<PutawayRecordDto> ToDtoAsync(PutawayRecord record, CancellationToken cancellationToken)
    {
        var item = await dbContext.PutawayItems.AsNoTracking().FirstAsync(i => i.Id == record.ItemId, cancellationToken);
        var binCode = await dbContext.Bins.AsNoTracking().Where(b => b.Id == record.BinId).Select(b => b.BinCode).FirstAsync(cancellationToken);
        var sku = item.VariantRefId is { } id ? (await variants.FindByIdAsync(id, cancellationToken))?.SkuCode : null;
        return new PutawayRecordDto(record.Id, item.Id, record.BinId, binCode, record.Quantity, record.Scanned, record.ActorName, record.InsrDte, item.Pending, sku);
    }
}
