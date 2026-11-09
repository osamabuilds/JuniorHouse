using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain.Reservations;

namespace Romp.Modules.Warehouse.Application.Reservations;

/// <summary>Reasons recorded on the stock events a reservation causes.</summary>
public static class ReservationReasons
{
    public const string Reserved = "RESERVED";
    public const string Released = "RELEASED";
    public const string Expired = "RESERVATION_EXPIRED";
    public const string Picked = "PICKED";
}

/// <summary>Reserve stock for one order, all lines or none, idempotent on the placement key (RSV-1 to RSV-4).</summary>
public sealed record ReserveStockCommand(string PlacementKey, IReadOnlyList<ReservationLineRequest> Lines) : IRequest<ReservationResult>, IWarehouseCommand;

public sealed class ReserveStockCommandHandler(IWarehouseDbContext dbContext, IReservationLedger ledger, TimeProvider clock, IOptions<WarehouseOptions> options)
    : IRequestHandler<ReserveStockCommand, ReservationResult>
{
    public async Task<ReservationResult> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Lines);
        if (request.Lines.Count is 0 or > IStockReservations.MaxLines)
        {
            throw new DomainException($"A reservation has between 1 and {IStockReservations.MaxLines} different variants.");
        }

        // The same key again is the same reservation (RSV-4), whatever the lines in the repeat say.
        var existing = await dbContext.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey, cancellationToken);
        if (existing is not null)
        {
            return existing.StatusId == ReservationStatuses.Released
                ? new ReservationResult(ReservationOutcome.Released, existing.Id, [])
                : new ReservationResult(ReservationOutcome.Reserved, existing.Id, []);
        }

        // Labelling's variant ids to warehouse reference ids in one query. A variant the warehouse has never heard of has nothing to reserve.
        var ids = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var refs = await dbContext.VariantRefs.AsNoTracking().Where(v => ids.Contains(v.VariantId)).Select(v => new { v.Id, v.VariantId }).ToDictionaryAsync(v => v.VariantId, v => v.Id, cancellationToken);
        var unknown = request.Lines.Where(l => !refs.ContainsKey(l.VariantId)).Select(l => new ShortageLine(l.VariantId, l.Quantity, 0)).ToList();
        if (unknown.Count > 0)
        {
            return new ReservationResult(ReservationOutcome.Shortage, null, unknown);
        }

        var hold = clock.GetUtcNow().AddMinutes(options.Value.ReservationHoldMinutes);
        var reservation = StockReservation.Create(request.PlacementKey, request.Lines.Select(l => (refs[l.VariantId], l.Quantity)), hold);
        var lines = reservation.Lines.Select(l => new ReservedLine(l.VariantRefId, l.Quantity)).ToList();

        var shortages = await ledger.TryReserveAllAsync(lines, ReservationReasons.Reserved, cancellationToken);
        if (shortages.Count > 0)
        {
            var byRef = refs.ToDictionary(kv => kv.Value, kv => kv.Key);
            return new ReservationResult(ReservationOutcome.Shortage, null, [.. shortages.Select(s => new ShortageLine(byRef[s.VariantRefId], s.Requested, s.Available))]);
        }

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ReservationResult(ReservationOutcome.Reserved, reservation.Id, []);
    }
}

/// <summary>Give an order's reserved units back, once (RSV-5). Releasing an unknown or already released key does nothing.</summary>
public sealed record ReleaseReservationCommand(string PlacementKey, string ReasonCode = ReservationReasons.Released) : IRequest<bool>, IWarehouseCommand;

public sealed class ReleaseReservationCommandHandler(IWarehouseDbContext dbContext, IReservationLedger ledger)
    : IRequestHandler<ReleaseReservationCommand, bool>
{
    public async Task<bool> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey, cancellationToken);
        if (reservation is null || reservation.Release() != ReservationTransition.Changed)
        {
            return false;
        }

        await ledger.ReleaseAsync([.. reservation.Lines.OrderBy(l => l.VariantRefId).Select(l => new ReservedLine(l.VariantRefId, l.Quantity))], request.ReasonCode, cancellationToken);
        return true;
    }
}
