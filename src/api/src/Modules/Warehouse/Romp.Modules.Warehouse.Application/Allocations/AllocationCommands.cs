using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Allocations;
using Romp.Modules.Warehouse.Domain.Reservations;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.Allocations;

/// <summary>One bin that holds free units of a variant, with the date its current stock was first shelved (the FIFO key, Q-ALC-1).</summary>
public sealed record BinCandidate(long BinId, short GradeId, string BinCode, string Zone, string Aisle, string Shelf, int Free, DateTimeOffset Since);

/// <summary>Implemented by Infrastructure with raw SQL: locking variant rows and finding bins with free units, oldest first.</summary>
public interface IAllocationCandidates
{
    /// <summary>Takes the variant row locks in ascending id order, so two allocations of one variant take turns and never deadlock (ALC-9).</summary>
    Task LockVariantsAsync(IReadOnlyList<long> variantRefIds, CancellationToken cancellationToken);

    /// <summary>Bins with units not yet claimed by an active allocation, skipping inactive bins and bins under review (PEX-6), oldest putaway first.</summary>
    Task<IReadOnlyList<BinCandidate>> FindAsync(long variantRefId, CancellationToken cancellationToken);
}

public static class AllocationWalking
{
    /// <summary>Sorts bins in walking order: zone, then aisle as a number, then shelf.</summary>
    public static string WalkKey(string zone, string aisle, string shelf) => $"{zone.PadRight(3)}-{aisle.PadLeft(3, '0')}-{shelf.PadRight(2)}";
}

/// <summary>Allocate an order's lines to bins, oldest putaway first, or top up what was short (ALC-3 to ALC-9).</summary>
public sealed record AllocateStockCommand(string OrderKey, IReadOnlyList<AllocationLineRequest> Lines) : IRequest<AllocationResult>, IWarehouseCommand;

public sealed class AllocateStockCommandHandler(IWarehouseDbContext dbContext, IAllocationCandidates candidates)
    : IRequestHandler<AllocateStockCommand, AllocationResult>
{
    public const int MaxQuantityPerLine = 1000;

    public async Task<AllocationResult> Handle(AllocateStockCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Lines);
        if (request.Lines.Count is 0 or > IStockAllocator.MaxLines)
        {
            throw new DomainException($"An allocation has between 1 and {IStockAllocator.MaxLines} different variants.");
        }

        if (request.Lines.Any(l => l.Quantity is <= 0 or > MaxQuantityPerLine))
        {
            throw new DomainException($"Each allocated quantity is between 1 and {MaxQuantityPerLine}.");
        }

        var wanted = request.Lines.GroupBy(l => l.VariantId).Select(g => (VariantId: g.Key, Quantity: g.Sum(l => l.Quantity))).ToList();
        var ids = wanted.Select(w => w.VariantId).ToList();
        var variants = await dbContext.VariantRefs.AsNoTracking().Where(v => ids.Contains(v.VariantId))
            .Select(v => new { v.Id, v.VariantId, v.Gtin, v.SkuCode }).ToDictionaryAsync(v => v.VariantId, cancellationToken);

        await candidates.LockVariantsAsync([.. variants.Values.Select(v => v.Id).Order()], cancellationToken);

        var allocation = await dbContext.Allocations.Include(a => a.Lines).FirstOrDefaultAsync(a => a.OrderKey == request.OrderKey, cancellationToken);
        if (allocation is null)
        {
            allocation = new StockAllocation(request.OrderKey);
            dbContext.Allocations.Add(allocation);
        }
        else if (!allocation.IsActive)
        {
            return new AllocationResult([], [], IsClosed: true);
        }

        var shortages = new List<ShortAllocation>();
        foreach (var (variantId, quantity) in wanted)
        {
            if (!variants.TryGetValue(variantId, out var variant))
            {
                shortages.Add(new ShortAllocation(variantId, quantity, 0));
                continue;
            }

            var need = quantity - allocation.ClaimedFor(variant.Id);
            if (need > 0)
            {
                var bins = await candidates.FindAsync(variant.Id, cancellationToken);
                foreach (var bin in bins.Where(b => b.Free > 0).OrderBy(b => b.Since).ThenBy(b => b.BinCode, StringComparer.Ordinal).ThenBy(b => b.GradeId))
                {
                    if (need == 0)
                    {
                        break;
                    }

                    var take = Math.Min(need, bin.Free);
                    allocation.Claim(variant.Id, bin.GradeId, bin.BinId, take);
                    need -= take;
                }
            }

            if (need > 0)
            {
                shortages.Add(new ShortAllocation(variantId, quantity, quantity - need));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var binIds = allocation.Lines.Select(l => l.BinId).Distinct().ToList();
        var bins2 = await dbContext.Bins.AsNoTracking().Where(b => binIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BinCode, b.Zone, b.Aisle, b.Shelf }).ToDictionaryAsync(b => b.Id, cancellationToken);
        var byRef = variants.Values.ToDictionary(v => v.Id);
        var units = allocation.Lines
            .Where(l => byRef.ContainsKey(l.VariantRefId))
            .Select(l =>
            {
                var variant = byRef[l.VariantRefId];
                var bin = bins2[l.BinId];
                return new AllocatedUnit(variant.VariantId, bin.Id, bin.BinCode, AllocationWalking.WalkKey(bin.Zone, bin.Aisle, bin.Shelf), l.Quantity, variant.Gtin, variant.SkuCode);
            })
            .OrderBy(u => u.WalkKey, StringComparer.Ordinal).ThenBy(u => u.VariantId)
            .ToList();
        return new AllocationResult(units, shortages);
    }
}

/// <summary>Give an order's allocation back (ALC-6). Unknown, released or picked keys do nothing.</summary>
public sealed record ReleaseAllocationCommand(string OrderKey) : IRequest<bool>, IWarehouseCommand;

public sealed class ReleaseAllocationCommandHandler(IWarehouseDbContext dbContext) : IRequestHandler<ReleaseAllocationCommand, bool>
{
    public async Task<bool> Handle(ReleaseAllocationCommand request, CancellationToken cancellationToken)
    {
        var allocation = await dbContext.Allocations.FirstOrDefaultAsync(a => a.OrderKey == request.OrderKey, cancellationToken);
        return allocation is not null && allocation.Release();
    }
}

/// <summary>Turn an order's picked units into a ledger decrement and convert its reservation, once (PCK-7, PCK-8, RSV-9).</summary>
public sealed record ConfirmPickCommand(string OrderKey, IReadOnlyList<PickedUnit> Picks, string Staff) : IRequest<bool>, IWarehouseCommand;

public sealed class ConfirmPickCommandHandler(IWarehouseDbContext dbContext, IStockPosting posting, IReservationLedger reservations)
    : IRequestHandler<ConfirmPickCommand, bool>
{
    public async Task<bool> Handle(ConfirmPickCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Picks);
        if (request.Picks.Any(p => p.Quantity <= 0))
        {
            throw new DomainException("A picked quantity is at least one unit.");
        }

        var allocation = await dbContext.Allocations.Include(a => a.Lines).FirstOrDefaultAsync(a => a.OrderKey == request.OrderKey, cancellationToken)
            ?? throw new DomainException("There is no allocation for this order.");
        if (allocation.StatusId == AllocationStatuses.Picked)
        {
            return false;
        }

        if (!allocation.IsActive)
        {
            throw new DomainException("This allocation was released, so nothing can be picked from it.");
        }

        var ids = request.Picks.Select(p => p.VariantId).Distinct().ToList();
        var refs = await dbContext.VariantRefs.AsNoTracking().Where(v => ids.Contains(v.VariantId)).ToDictionaryAsync(v => v.VariantId, v => v.Id, cancellationToken);

        // Every pick must sit inside the allocation: the same variant, the same bin, no more than was set aside there.
        var posts = new List<StockPost>();
        foreach (var group in request.Picks.GroupBy(p => (p.VariantId, p.BinId)))
        {
            if (!refs.TryGetValue(group.Key.VariantId, out var refId))
            {
                throw new DomainException("A picked variant is not known to the warehouse.");
            }

            var remaining = group.Sum(p => p.Quantity);
            foreach (var line in allocation.Lines.Where(l => l.VariantRefId == refId && l.BinId == group.Key.BinId).OrderBy(l => l.GradeId))
            {
                var take = Math.Min(remaining, line.Quantity);
                if (take > 0)
                {
                    posts.Add(new StockPost(refId, line.GradeId, line.BinId, -take, MovementKinds.Pick, allocation.Id));
                    remaining -= take;
                }
            }

            if (remaining > 0)
            {
                throw new DomainException("A pick is more than was allocated in that bin.");
            }
        }

        // The reservation is converted first: on-hand may not drop below what is promised to orders (RSV-8), and these units are no longer promised.
        var reservation = await dbContext.Reservations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.PlacementKey == request.OrderKey, cancellationToken);
        if (reservation is not null && reservation.Convert() == ReservationTransition.Changed)
        {
            await reservations.ReleaseAsync([.. reservation.Lines.OrderBy(l => l.VariantRefId).Select(l => new ReservedLine(l.VariantRefId, l.Quantity))], ReservationReasons.Picked, cancellationToken);
        }

        if (posts.Count > 0)
        {
            await posting.PostAsync(posts, request.Staff, cancellationToken);
        }

        allocation.MarkPicked();
        return true;
    }
}

/// <summary>Mark a bin "under review", or clear the mark (PEX-6). Stock is never changed by this.</summary>
public sealed record SetBinReviewCommand(long BinId, string? ReasonCode) : IRequest<bool>, IWarehouseCommand;

public sealed class SetBinReviewCommandHandler(IWarehouseDbContext dbContext) : IRequestHandler<SetBinReviewCommand, bool>
{
    public async Task<bool> Handle(SetBinReviewCommand request, CancellationToken cancellationToken)
    {
        var bin = await dbContext.Bins.FirstOrDefaultAsync(b => b.Id == request.BinId, cancellationToken);
        if (bin is null)
        {
            return false;
        }

        if (request.ReasonCode is null)
        {
            bin.ClearReview();
        }
        else
        {
            bin.FlagForReview(request.ReasonCode);
        }

        return true;
    }
}
