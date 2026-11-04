using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Warehouse.Application.Reservations;

/// <summary>A variant (by warehouse reference id) and a quantity to reserve or give back.</summary>
public sealed record ReservedLine(long VariantRefId, int Quantity);

/// <summary>
/// The only writer of the reserved quantity per variant (ADR 0025, like <c>IStockPosting</c> for bins). Runs inside the caller's
/// transaction. Each change is one conditional SQL statement, never read-modify-write, and lines are always taken in ascending variant
/// order, so two reservations cannot deadlock. Each touched variant gets a higher stock version and one <c>StockLevelChangedEvent</c>
/// carrying the new available figure (RSV-7).
/// </summary>
public interface IReservationLedger
{
    /// <summary>
    /// Reserves every line or none: when any line cannot be covered, everything this call did is undone and the shortfalls are returned
    /// (empty means all were reserved). <paramref name="lines"/> must be sorted by variant reference id.
    /// </summary>
    Task<IReadOnlyList<ReservationShortage>> TryReserveAllAsync(IReadOnlyList<ReservedLine> lines, string reasonCode, CancellationToken cancellationToken);

    /// <summary>Gives units back (the reservation was released). Throws if it would take a variant's reserved quantity below zero.</summary>
    Task ReleaseAsync(IReadOnlyList<ReservedLine> lines, string reasonCode, CancellationToken cancellationToken);
}

/// <summary>A line that could not be covered, in warehouse reference ids.</summary>
public sealed record ReservationShortage(long VariantRefId, int Requested, int Available);
