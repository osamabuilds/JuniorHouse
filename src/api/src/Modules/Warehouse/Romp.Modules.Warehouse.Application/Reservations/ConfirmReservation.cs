using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain.Reservations;

namespace Romp.Modules.Warehouse.Application.Reservations;

/// <summary>What confirming a reservation did.</summary>
public enum ConfirmOutcome
{
    /// <summary>The hold became firm, or it already was.</summary>
    Confirmed,

    /// <summary>The hold had been released before the order event arrived, and the stock was reserved again.</summary>
    Reinstated,
}

/// <summary>
/// The order exists: its hold becomes a firm reservation (ADR 0025 step 7). Repeating it changes nothing. If the hold was already released
/// (the order event came after the sweeper, ADR 0025 consequences), the same lines are reserved again; if stock no longer allows, this throws,
/// so the dispatcher retries and then dead-letters the message where staff see it: an oversold order is a human problem, not a silent one.
/// </summary>
public sealed record ConfirmReservationCommand(string PlacementKey, string OrderNumber) : IRequest<ConfirmOutcome>, IWarehouseCommand;

public sealed class ConfirmReservationCommandHandler(IWarehouseDbContext dbContext, IReservationLedger ledger) : IRequestHandler<ConfirmReservationCommand, ConfirmOutcome>
{
    public async Task<ConfirmOutcome> Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey, cancellationToken)
            ?? throw new DomainException($"Order {request.OrderNumber} has no stock reservation to confirm.");

        switch (reservation.Confirm(request.OrderNumber))
        {
            case ReservationTransition.Changed or ReservationTransition.Unchanged:
                return ConfirmOutcome.Confirmed;
            default:
                var lines = reservation.Lines.OrderBy(l => l.VariantRefId).Select(l => new ReservedLine(l.VariantRefId, l.Quantity)).ToList();
                var shortages = await ledger.TryReserveAllAsync(lines, ReservationReasons.Reserved, cancellationToken);
                if (shortages.Count > 0)
                {
                    throw new DomainException($"Order {request.OrderNumber} was placed but its stock hold had expired and the stock is no longer available. Staff must resolve it.");
                }

                reservation.Reinstate(request.OrderNumber);
                return ConfirmOutcome.Reinstated;
        }
    }
}
