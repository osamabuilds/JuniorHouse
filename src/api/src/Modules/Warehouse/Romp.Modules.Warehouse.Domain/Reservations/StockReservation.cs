using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain.Reservations;

/// <summary>WHSE.RSRV_STS_LKP</summary>
public static class ReservationStatuses
{
    public const short Held = 1;
    public const short Confirmed = 2;
    public const short Converted = 3;
    public const short Released = 4;

    public static bool IsKnown(short id) => id is Held or Confirmed or Converted or Released;
}

/// <summary>WHSE.RSRV_STS_LKP</summary>
public sealed class ReservationStatusLookup : WarehouseLookup;

/// <summary>What asking a reservation to change state did. A repeat is <see cref="Unchanged"/> and harmless (RSV-4, RSV-5).</summary>
public enum ReservationTransition
{
    Changed,
    Unchanged,

    /// <summary>The state does not allow it (a released reservation cannot be confirmed; a converted one cannot be released).</summary>
    NotAllowed,
}

/// <summary>One variant and how many units of it are promised to an order (WHSE.STCK_RSRV_LINE).</summary>
public sealed class ReservationLine : Entity<long>, IAuditable
{
    private ReservationLine()
    {
        InsrBy = string.Empty;
    }

    internal ReservationLine(long variantRefId, int quantity)
    {
        VariantRefId = variantRefId;
        Quantity = quantity;
        InsrBy = string.Empty;
    }

    public long ReservationId { get; private set; }

    public long VariantRefId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// Units promised to one order (WHSE.STCK_RSRV), found by the order's placement key, which is unique: asking twice for the same key is the
/// same reservation (RSV-4). The state moves <c>Held → Confirmed → Converted</c>, or <c>Held | Confirmed → Released</c>; each step
/// reports <see cref="ReservationTransition.Unchanged"/> when repeated, so a redelivered event changes nothing (RSV-5). The reserved
/// quantity per variant lives in <c>STCK_RSRV_BAL</c>, written by the reservation port only (ADR 0025).
/// </summary>
public sealed class StockReservation : AggregateRoot<long>, IAuditable
{
    public const int MaxLines = 100;
    public const int MaxQuantityPerLine = 1000;
    public const int MaxKeyLength = 64;

    private readonly List<ReservationLine> _lines = [];

    private StockReservation()
    {
        PlacementKey = string.Empty;
        InsrBy = string.Empty;
    }

    public string PlacementKey { get; private set; }

    public short StatusId { get; private set; } = ReservationStatuses.Held;

    /// <summary>When an unconfirmed hold may be given back by the sweeper. Set only while <see cref="ReservationStatuses.Held"/>.</summary>
    public DateTimeOffset? HeldUntil { get; private set; }

    public string? OrderNumber { get; private set; }

    public IReadOnlyList<ReservationLine> Lines => _lines;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Lines are merged by variant and sorted by variant, the fixed order every locking step uses to avoid deadlocks.</summary>
    public static StockReservation Create(string placementKey, IEnumerable<(long VariantRefId, int Quantity)> lines, DateTimeOffset heldUntil)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (string.IsNullOrWhiteSpace(placementKey) || placementKey.Length > MaxKeyLength)
        {
            throw new DomainException($"A reservation needs a placement key of up to {MaxKeyLength} characters.");
        }

        var merged = lines.GroupBy(l => l.VariantRefId).Select(g => (VariantRefId: g.Key, Quantity: g.Sum(l => l.Quantity))).OrderBy(l => l.VariantRefId).ToList();
        if (merged.Count is 0 or > MaxLines)
        {
            throw new DomainException($"A reservation has between 1 and {MaxLines} different variants.");
        }

        if (merged.Any(l => l.VariantRefId <= 0 || l.Quantity <= 0 || l.Quantity > MaxQuantityPerLine))
        {
            throw new DomainException($"Each reserved quantity is between 1 and {MaxQuantityPerLine}.");
        }

        var reservation = new StockReservation { PlacementKey = placementKey, HeldUntil = heldUntil };
        reservation._lines.AddRange(merged.Select(l => new ReservationLine(l.VariantRefId, l.Quantity)));
        return reservation;
    }

    /// <summary>The order exists: the hold becomes firm and no longer expires.</summary>
    public ReservationTransition Confirm(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("Confirming a reservation needs the order number.");
        }

        switch (StatusId)
        {
            case ReservationStatuses.Held:
                StatusId = ReservationStatuses.Confirmed;
                OrderNumber = orderNumber;
                HeldUntil = null;
                return ReservationTransition.Changed;
            case ReservationStatuses.Confirmed or ReservationStatuses.Converted:
                return ReservationTransition.Unchanged;
            default:
                return ReservationTransition.NotAllowed;
        }
    }

    /// <summary>Gives the units back. Once released, releasing again does nothing (RSV-5); once picked it cannot be released here.</summary>
    public ReservationTransition Release()
    {
        switch (StatusId)
        {
            case ReservationStatuses.Held or ReservationStatuses.Confirmed:
                StatusId = ReservationStatuses.Released;
                HeldUntil = null;
                return ReservationTransition.Changed;
            case ReservationStatuses.Released:
                return ReservationTransition.Unchanged;
            default:
                return ReservationTransition.NotAllowed;
        }
    }

    /// <summary>The pick was confirmed and the ledger decremented (RSV-9, built in S14).</summary>
    public ReservationTransition Convert()
    {
        switch (StatusId)
        {
            case ReservationStatuses.Confirmed:
                StatusId = ReservationStatuses.Converted;
                return ReservationTransition.Changed;
            case ReservationStatuses.Converted:
                return ReservationTransition.Unchanged;
            default:
                return ReservationTransition.NotAllowed;
        }
    }
}

/// <summary>
/// Units promised to orders for one variant (WHSE.STCK_RSRV_BAL), keyed by the variant. Available = on-hand minus this. Changed only by
/// the conditional SQL of the reservation port, like the bin balances are changed only by posting (ADR 0012, ADR 0025).
/// </summary>
public sealed class ReservedBalance : Entity<long>, IAuditable
{
    /// <summary>The variant reference id (this row's key).</summary>
    public long VariantRefId => Id;

    public int Quantity { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
