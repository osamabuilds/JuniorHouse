using MediatR;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Warehouse.Infrastructure.Reservations;

/// <summary>The Warehouse side of <see cref="IStockReservations"/>: each call is one command, so it runs in its own Warehouse transaction (ADR 0025).</summary>
public sealed class StockReservations(ISender sender) : IStockReservations
{
    public Task<ReservationResult> ReserveAsync(string placementKey, IReadOnlyList<ReservationLineRequest> lines, CancellationToken cancellationToken) =>
        sender.Send(new ReserveStockCommand(placementKey, lines), cancellationToken);

    public async Task ReleaseAsync(string placementKey, CancellationToken cancellationToken) =>
        await sender.Send(new ReleaseReservationCommand(placementKey), cancellationToken);
}
