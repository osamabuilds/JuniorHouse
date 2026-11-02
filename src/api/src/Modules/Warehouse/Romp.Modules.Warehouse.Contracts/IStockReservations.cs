namespace Romp.Modules.Warehouse.Contracts;

/// <summary>One variant and how many units to promise to an order. <c>VariantId</c> is Labelling's variant id, as in the stock events.</summary>
public sealed record ReservationLineRequest(long VariantId, int Quantity);

/// <summary>A variant that could not be covered: how many were wanted and how many are available (on-hand minus reserved).</summary>
public sealed record ShortageLine(long VariantId, int Requested, int Available);

public enum ReservationOutcome
{
    /// <summary>Every line is reserved (or was already, for the same placement key).</summary>
    Reserved,

    /// <summary>At least one line is short; nothing was reserved (RSV-2).</summary>
    Shortage,

    /// <summary>The placement key was reserved before and has since been released, so it is not reserved again under that key.</summary>
    Released,
}

public sealed record ReservationResult(ReservationOutcome Outcome, long? ReservationId, IReadOnlyList<ShortageLine> Shortages)
{
    public bool IsReserved => Outcome == ReservationOutcome.Reserved;
}

/// <summary>
/// How Orders keeps stock for an order (ADR 0025, RSV-1 to RSV-5). Reserving is all lines or none and idempotent on the placement key; no
/// request or answer carries personal data. A hold that no order confirms is released by Warehouse itself after
/// <c>Warehouse:ReservationHoldMinutes</c>.
/// </summary>
public interface IStockReservations
{
    /// <summary>The most different variants one reservation may hold.</summary>
    public const int MaxLines = 100;

    Task<ReservationResult> ReserveAsync(string placementKey, IReadOnlyList<ReservationLineRequest> lines, CancellationToken cancellationToken);

    /// <summary>Gives the units back once. Releasing an unknown, already released or never-reserved key does nothing (RSV-5).</summary>
    Task ReleaseAsync(string placementKey, CancellationToken cancellationToken);
}
