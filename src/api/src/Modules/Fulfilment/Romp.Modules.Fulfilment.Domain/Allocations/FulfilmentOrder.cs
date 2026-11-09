using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Contracts;
using Romp.Modules.Fulfilment.Domain.PackStation;
using Romp.Modules.Fulfilment.Domain.PickExceptions;

namespace Romp.Modules.Fulfilment.Domain.Allocations;

/// <summary>What the pick or pack station answers to a scan. A refusal changes nothing (PCK-3, PCK-4, PAK-2) and is told in words.</summary>
public sealed record ScanResult(bool Accepted, string Code, string Message, string? NextBin = null);

/// <summary>One order line to ship: what Orders recorded, merged by variant.</summary>
public sealed record FulfilmentLineSeed(long VariantId, string? Sku, string Name, string Colour, string Size, int Quantity, decimal UnitPrice);

/// <summary>One bin claim handed back by Warehouse for a variant.</summary>
public sealed record AllocationSeed(long VariantId, long BinId, string BinCode, string WalkKey, int Quantity, string Gtin, string Sku);

/// <summary>Units of one line claimed in one bin (FULF.FULF_ALOC). A line may have several, oldest putaway first (ALC-4).</summary>
public sealed class LineAllocation : Entity<long>, IAuditable
{
    private LineAllocation()
    {
        BinCode = string.Empty;
        WalkKey = string.Empty;
        InsrBy = string.Empty;
    }

    internal LineAllocation(long binId, string binCode, string walkKey, int quantity)
    {
        BinId = binId;
        BinCode = binCode;
        WalkKey = walkKey;
        Quantity = quantity;
        InsrBy = string.Empty;
    }

    public long LineId { get; private set; }

    public long BinId { get; private set; }

    public string BinCode { get; private set; }

    public string WalkKey { get; private set; }

    public int Quantity { get; internal set; }

    public int PickedQuantity { get; internal set; }

    public int Remaining => Quantity - PickedQuantity;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>A snapshot of one thing to ship (FULF.FULF_LINE): the variant, its barcode, how many, and how far picking and packing have got.</summary>
public sealed class FulfilmentLine : Entity<long>, IAuditable
{
    private readonly List<LineAllocation> _allocations = [];

    private FulfilmentLine()
    {
        Name = string.Empty;
        Colour = string.Empty;
        Size = string.Empty;
        InsrBy = string.Empty;
    }

    internal FulfilmentLine(FulfilmentLineSeed seed)
    {
        VariantId = seed.VariantId;
        Sku = seed.Sku;
        Name = seed.Name;
        Colour = seed.Colour;
        Size = seed.Size;
        Quantity = seed.Quantity;
        UnitPrice = seed.UnitPrice;
        StatusId = LineStatuses.AwaitingRestock;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public long VariantId { get; private set; }

    public string? Sku { get; internal set; }

    public string? Gtin { get; internal set; }

    public string Name { get; private set; }

    public string Colour { get; private set; }

    public string Size { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public short StatusId { get; internal set; }

    public int PickedQuantity { get; internal set; }

    public int PackedQuantity { get; internal set; }

    public IReadOnlyList<LineAllocation> Allocations => _allocations;

    public bool IsActive => StatusId != LineStatuses.Cancelled;

    public int AllocatedQuantity => _allocations.Sum(a => a.Quantity);

    internal List<LineAllocation> AllocationList => _allocations;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// One order on its way from "confirmed" to "packed" (FULF.FULF_ORDR, ADR 0030). The aggregate guards every rule of picking and packing:
/// a scan never counts more than was allocated (PCK-5), only the picker who holds the order may scan (PCK-9), only a fully picked order can be
/// packed (PAK-10), and a repeated step changes nothing (ALC-5, PCK-8, PAK-13). It holds snapshots only: ids, codes and the order number; no
/// customer data (SEC-25). Scans that are refused are answered, never thrown, and leave the order untouched.
/// </summary>
public sealed class FulfilmentOrder : AggregateRoot<long>, IAuditable
{
    public const int MaxCodeLength = 40;

    private readonly List<FulfilmentLine> _lines = [];
    private readonly List<PickExceptionRecord> _exceptions = [];

    private FulfilmentOrder()
    {
        OrderNumber = string.Empty;
        PlacementKey = string.Empty;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public string OrderNumber { get; private set; }

    /// <summary>The key Warehouse knows the allocation (and the stock reservation) by.</summary>
    public string PlacementKey { get; private set; }

    public short StatusId { get; private set; } = FulfilmentStatuses.Queued;

    public bool IsCashOnDelivery { get; private set; }

    public decimal CashToCollect { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public string? PickClaimedBy { get; private set; }

    public DateTimeOffset? PickClaimedAt { get; private set; }

    /// <summary>The bin the picker last scanned; a unit scan counts against it.</summary>
    public string? CurrentBin { get; private set; }

    public DateTimeOffset? PickedAt { get; private set; }

    public string? PickedBy { get; private set; }

    public string? PackClaimedBy { get; private set; }

    public DateTimeOffset? PackedAt { get; private set; }

    public string? PackedBy { get; private set; }

    /// <summary>True when the order was cancelled after some units had been scanned or picked: they must be put back on the shelf (ALC-7).</summary>
    public bool PutBackRequired { get; private set; }

    public PackRecord? Pack { get; private set; }

    /// <summary>How many times the packing slip was sent to the printer (a second print is a reprint, SLP-5).</summary>
    public int SlipPrintCount { get; private set; }

    public DateTimeOffset? SlipPrintedAt { get; private set; }

    /// <summary>The printer was down: the slip prints on retry and the pack is not blocked (SLP-6).</summary>
    public bool SlipPrintPending { get; private set; }

    public IReadOnlyList<FulfilmentLine> Lines => _lines;

    public IReadOnlyList<PickExceptionRecord> Exceptions => _exceptions;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public int TotalUnits => _lines.Where(l => l.IsActive).Sum(l => l.Quantity);

    public IEnumerable<FulfilmentLine> ActiveLines => _lines.Where(l => l.IsActive);

    // ---------------------------------------------------------------- creation and allocation (ALC-1, 2, 5, 8)

    public static FulfilmentOrder Create(long orderId, string orderNumber, string placementKey, bool isCashOnDelivery, decimal cashToCollect, DateTimeOffset at, IEnumerable<FulfilmentLineSeed> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(placementKey) || placementKey.Length > 64)
        {
            throw new DomainException("A fulfilment order needs an order number and a placement key.");
        }

        var merged = lines.GroupBy(l => l.VariantId)
            .Select(g => g.First() with { Quantity = g.Sum(l => l.Quantity) })
            .OrderBy(l => l.VariantId)
            .ToList();
        if (merged.Count is 0 or > 100 || merged.Any(l => l.Quantity <= 0))
        {
            throw new DomainException("A fulfilment order has between 1 and 100 lines of at least one unit.");
        }

        var order = new FulfilmentOrder
        {
            OrderId = orderId,
            OrderNumber = orderNumber,
            PlacementKey = placementKey,
            IsCashOnDelivery = isCashOnDelivery,
            CashToCollect = cashToCollect,
            QueuedAt = at,
        };
        order._lines.AddRange(merged.Select(l => new FulfilmentLine(l)));
        return order;
    }

    /// <summary>
    /// Takes what Warehouse allocated (all claims for the order's variants) as the lines' bin locations. Units already picked stay counted against
    /// the same bin. A line the bins cannot fully cover waits for restock and the customer is told (ALC-8). Safe to call again after a restock.
    /// </summary>
    public void ApplyAllocation(IReadOnlyList<AllocationSeed> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        foreach (var line in _lines.Where(l => l.IsActive))
        {
            var mine = claims.Where(c => c.VariantId == line.VariantId).ToList();
            if (mine.Count > 0)
            {
                line.Gtin = mine[0].Gtin;
                line.Sku ??= mine[0].Sku;
            }

            var previous = line.AllocationList.ToDictionary(a => a.BinId, a => a.PickedQuantity);
            line.AllocationList.Clear();
            foreach (var claim in mine.OrderBy(c => c.WalkKey, StringComparer.Ordinal))
            {
                line.AllocationList.Add(new LineAllocation(claim.BinId, claim.BinCode, claim.WalkKey, claim.Quantity)
                {
                    PickedQuantity = Math.Min(claim.Quantity, previous.GetValueOrDefault(claim.BinId)),
                });
            }

            var wasWaiting = line.StatusId == LineStatuses.AwaitingRestock;
            var covered = line.AllocatedQuantity >= line.Quantity;
            if (line.PickedQuantity >= line.Quantity && covered)
            {
                line.StatusId = LineStatuses.Picked;
            }
            else if (covered)
            {
                line.StatusId = LineStatuses.Allocated;
            }
            else
            {
                line.StatusId = LineStatuses.AwaitingRestock;
                if (!wasWaiting || line.Id == 0)
                {
                    Raise(new OrderLineDelayedEvent(OrderId, OrderNumber, line.VariantId, "NO_STOCK"));
                }
            }
        }

        RefreshQueueStatus();
    }

    /// <summary>The pick queue shows an order unless every active line is waiting for restock (PEX-7).</summary>
    private void RefreshQueueStatus()
    {
        if (StatusId is not (FulfilmentStatuses.Queued or FulfilmentStatuses.Held))
        {
            return;
        }

        var active = _lines.Where(l => l.IsActive).ToList();
        StatusId = active.Count > 0 && active.All(l => l.StatusId == LineStatuses.AwaitingRestock) ? FulfilmentStatuses.Held : FulfilmentStatuses.Queued;
    }

    // ---------------------------------------------------------------- cancel (ALC-6, ALC-7, ALC-11)

    /// <summary>
    /// The order was cancelled. Before anything is picked, it simply stops. If units were scanned (but the pick was not completed) they must be put
    /// back (ALC-7). Once the pick is completed and stock decremented, the order is left to the later stages (S15, S17); returns false then.
    /// </summary>
    public bool CancelFromOrder()
    {
        if (StatusId is FulfilmentStatuses.Cancelled)
        {
            return false;
        }

        if (StatusId is FulfilmentStatuses.Picked or FulfilmentStatuses.Packing or FulfilmentStatuses.Packed)
        {
            return false;
        }

        PutBackRequired = _lines.Any(l => l.PickedQuantity > 0);
        StatusId = FulfilmentStatuses.Cancelled;
        PickClaimedBy = null;
        PickClaimedAt = null;
        CurrentBin = null;
        return true;
    }

    // ---------------------------------------------------------------- picking (PCK-1 to PCK-12)

    /// <summary>The next bin that still has units to pick, in walking order.</summary>
    public string? NextBin() =>
        _lines.Where(l => l.StatusId == LineStatuses.Allocated).SelectMany(l => l.Allocations).Where(a => a.Remaining > 0)
            .OrderBy(a => a.WalkKey, StringComparer.Ordinal).Select(a => a.BinCode).FirstOrDefault();

    /// <summary>Takes the pick for this picker. A second picker is told who has it (PCK-9). Returns the holder's name when it is someone else.</summary>
    public string? Claim(string staff, DateTimeOffset at)
    {
        EnsureStaff(staff);
        if (StatusId is not (FulfilmentStatuses.Queued or FulfilmentStatuses.Picking))
        {
            throw new DomainException("This order is not waiting to be picked.");
        }

        if (PickClaimedBy is not null && !string.Equals(PickClaimedBy, staff, StringComparison.Ordinal))
        {
            return PickClaimedBy;
        }

        PickClaimedBy = staff;
        PickClaimedAt ??= at;
        StatusId = FulfilmentStatuses.Picking;
        return null;
    }

    /// <summary>The holder lets go (or a manager takes it over). Scans already made stay counted.</summary>
    public bool ReleaseClaim(string staff, bool isManager)
    {
        if (PickClaimedBy is null || StatusId != FulfilmentStatuses.Picking)
        {
            return false;
        }

        if (!isManager && !string.Equals(PickClaimedBy, staff, StringComparison.Ordinal))
        {
            throw new DomainException($"{PickClaimedBy} holds this pick.");
        }

        PickClaimedBy = null;
        PickClaimedAt = null;
        CurrentBin = null;
        StatusId = FulfilmentStatuses.Queued;
        RefreshQueueStatus();
        return true;
    }

    private ScanResult? PickGate(string staff)
    {
        if (StatusId == FulfilmentStatuses.Cancelled)
        {
            return new ScanResult(false, "ORDER_CANCELLED", "This order was cancelled. Put the items back on the shelf.");
        }

        if (StatusId != FulfilmentStatuses.Picking)
        {
            return new ScanResult(false, "NOT_PICKING", "This order is not being picked.");
        }

        return string.Equals(PickClaimedBy, staff, StringComparison.Ordinal)
            ? null
            : new ScanResult(false, "NOT_YOUR_PICK", $"{PickClaimedBy} holds this pick.");
    }

    /// <summary>The picker scans a shelf. It is accepted only when the order still needs units from it (PCK-4).</summary>
    public ScanResult ScanBin(string staff, string binCode)
    {
        if (PickGate(staff) is { } blocked)
        {
            return blocked;
        }

        var code = Normalise(binCode);
        var open = _lines.Where(l => l.StatusId == LineStatuses.Allocated).SelectMany(l => l.Allocations).Where(a => a.Remaining > 0).ToList();
        if (!open.Any(a => a.BinCode == code))
        {
            var next = NextBin();
            return new ScanResult(false, "BIN_NOT_ON_LIST", next is null ? $"Bin {code} is not on this pick list." : $"Bin {code} is not next. Go to {next}.", next);
        }

        CurrentBin = code;
        return new ScanResult(true, "BIN_OK", $"Bin {code}. Scan each unit.");
    }

    /// <summary>The picker scans a unit's barcode. It counts for the line at the scanned bin, never past the allocated quantity (PCK-2, 3, 5).</summary>
    public ScanResult ScanUnit(string staff, string gtin)
    {
        if (PickGate(staff) is { } blocked)
        {
            return blocked;
        }

        if (CurrentBin is null)
        {
            return new ScanResult(false, "SCAN_BIN_FIRST", "Scan the shelf first.", NextBin());
        }

        var code = Normalise(gtin);
        var line = _lines.FirstOrDefault(l => l.IsActive && l.Gtin == code);
        if (line is null)
        {
            return new ScanResult(false, "GTIN_NOT_ON_ORDER", "This item is not on the order. Put it back.");
        }

        if (line.StatusId == LineStatuses.AwaitingRestock)
        {
            return new ScanResult(false, "LINE_ON_HOLD", "This line is waiting for restock. Put it back.");
        }

        if (line.PickedQuantity >= line.Quantity)
        {
            return new ScanResult(false, "OVER_PICK", "This line is already complete. Put the extra unit back.");
        }

        var allocation = line.Allocations.FirstOrDefault(a => a.BinCode == CurrentBin && a.Remaining > 0);
        if (allocation is null)
        {
            var where = string.Join(", ", line.Allocations.Where(a => a.Remaining > 0).Select(a => a.BinCode));
            return new ScanResult(false, "WRONG_BIN", $"This item belongs in {where}, not {CurrentBin}.", line.Allocations.FirstOrDefault(a => a.Remaining > 0)?.BinCode);
        }

        allocation.PickedQuantity++;
        line.PickedQuantity++;
        if (line.PickedQuantity == line.Quantity)
        {
            line.StatusId = LineStatuses.Picked;
        }

        return new ScanResult(true, "UNIT_OK", $"{line.Name}, {line.Size}: {line.PickedQuantity} of {line.Quantity}.", NextBin());
    }

    /// <summary>True when every active line is picked, so the pick can be completed.</summary>
    public bool IsReadyToComplete => StatusId == FulfilmentStatuses.Picking && _lines.Any(l => l.IsActive) && _lines.Where(l => l.IsActive).All(l => l.StatusId == LineStatuses.Picked);

    /// <summary>
    /// Completes the pick. Returns the picks to hand to Warehouse for the ledger decrement, or null when this is a repeat (PCK-8). Refuses while a
    /// line is unpicked or waiting for restock.
    /// </summary>
    public IReadOnlyList<(long VariantId, long BinId, int Quantity)>? CompletePick(string staff, DateTimeOffset at)
    {
        if (StatusId is FulfilmentStatuses.Picked or FulfilmentStatuses.Packing or FulfilmentStatuses.Packed)
        {
            return null;
        }

        EnsureStaff(staff);
        if (StatusId != FulfilmentStatuses.Picking)
        {
            throw new DomainException("This order is not being picked.");
        }

        if (!string.Equals(PickClaimedBy, staff, StringComparison.Ordinal))
        {
            throw new DomainException($"{PickClaimedBy} holds this pick.");
        }

        if (_lines.Any(l => l.StatusId == LineStatuses.AwaitingRestock))
        {
            throw new DomainException("A line is waiting for restock. Resolve it or remove it before completing the pick.");
        }

        if (!IsReadyToComplete)
        {
            throw new DomainException("Not every unit has been scanned yet.");
        }

        var picks = _lines.Where(l => l.IsActive).SelectMany(l => l.Allocations.Where(a => a.PickedQuantity > 0).Select(a => (l.VariantId, a.BinId, a.PickedQuantity))).ToList();
        StatusId = FulfilmentStatuses.Picked;
        PickedAt = at;
        PickedBy = staff;
        PickClaimedBy = null;
        CurrentBin = null;
        Raise(new PickCompletedEvent(OrderId, OrderNumber));
        return picks;
    }

    // ---------------------------------------------------------------- exceptions (PEX-1 to PEX-10)

    /// <summary>
    /// The shelf does not hold what the system says (PEX-1). The units already scanned stay counted; the rest of the line waits for restock and the
    /// customer is told. The other lines can still be picked (PEX-2). Returns how many units of the bin's claim were given up, for Warehouse.
    /// </summary>
    public (PickExceptionRecord Exception, int GivenUp, long BinId) RaiseException(string staff, long variantId, string binCode, short reasonId, int foundQuantity, DateTimeOffset at)
    {
        if (PickGate(staff) is { } blocked)
        {
            throw new DomainException(blocked.Message);
        }

        if (!ExceptionReasons.IsKnown(reasonId))
        {
            throw new DomainException("Choose a reason: not found, short or damaged.");
        }

        var code = Normalise(binCode);
        var line = _lines.FirstOrDefault(l => l.IsActive && l.VariantId == variantId && l.StatusId == LineStatuses.Allocated)
            ?? throw new DomainException("That line is not waiting to be picked.");
        var allocation = line.Allocations.FirstOrDefault(a => a.BinCode == code && a.Remaining > 0)
            ?? throw new DomainException($"Nothing is left to pick from {code} for that line.");
        if (foundQuantity < 0 || foundQuantity > allocation.Quantity)
        {
            throw new DomainException($"The count found must be a whole number from 0 to {allocation.Quantity}.");
        }

        var systemCount = allocation.Remaining;
        var givenUp = systemCount;
        allocation.Quantity = allocation.PickedQuantity;
        if (allocation.Quantity == 0)
        {
            line.AllocationList.Remove(allocation);
        }

        line.StatusId = LineStatuses.AwaitingRestock;
        var exception = new PickExceptionRecord(variantId, allocation.BinId, code, reasonId, systemCount, foundQuantity, staff, at);
        _exceptions.Add(exception);
        CurrentBin = null;

        Raise(new OrderLineDelayedEvent(OrderId, OrderNumber, variantId, ExceptionReasons.CodeOf(reasonId)));
        Raise(new PickExceptionRaisedEvent(OrderId, OrderNumber, allocation.BinId, code, ExceptionReasons.CodeOf(reasonId)));

        if (_lines.Where(l => l.IsActive).All(l => l.StatusId == LineStatuses.AwaitingRestock))
        {
            StatusId = FulfilmentStatuses.Held;
            PickClaimedBy = null;
            PickClaimedAt = null;
        }

        return (exception, givenUp, allocation.BinId);
    }

    /// <summary>Stock arrived for a delayed line: the exception is resolved and the line must be allocated again by the caller (PEX-8).</summary>
    public FulfilmentLine ResolveRestocked(long exceptionId, string staff, DateTimeOffset at)
    {
        var exception = _exceptions.FirstOrDefault(e => e.Id == exceptionId) ?? throw new DomainException("There is no such exception on this order.");
        exception.Resolve(staff, at, restocked: true);
        return _lines.First(l => l.VariantId == exception.VariantId);
    }

    /// <summary>The customer would rather drop the delayed line: it leaves picking (PEX-9). A line with units already picked cannot be dropped here.</summary>
    public void ResolveCancelLine(long exceptionId, string staff, DateTimeOffset at)
    {
        var exception = _exceptions.FirstOrDefault(e => e.Id == exceptionId) ?? throw new DomainException("There is no such exception on this order.");
        var line = _lines.First(l => l.VariantId == exception.VariantId);
        if (line.PickedQuantity > 0)
        {
            throw new DomainException("Units of this line were already picked. Restock the line, or put the units back first.");
        }

        exception.Resolve(staff, at, restocked: false);
        line.StatusId = LineStatuses.Cancelled;
        line.AllocationList.Clear();
        if (!_lines.Any(l => l.IsActive))
        {
            StatusId = FulfilmentStatuses.Cancelled;
        }
        else
        {
            RefreshQueueStatus();
        }
    }

    /// <summary>Called after a restock allocation to put a Held order back in the queue (PEX-8).</summary>
    public void AfterRestock()
    {
        if (StatusId == FulfilmentStatuses.Held)
        {
            StatusId = FulfilmentStatuses.Queued;
        }

        RefreshQueueStatus();
    }

    // ---------------------------------------------------------------- packing (PAK-1 to PAK-13)

    public bool IsPackable => StatusId is FulfilmentStatuses.Picked or FulfilmentStatuses.Packing;

    /// <summary>Opens the order at the pack station. Only a fully picked order can be packed (PAK-10); a second packer is told who has it.</summary>
    public string? OpenPack(string staff)
    {
        EnsureStaff(staff);
        if (!IsPackable)
        {
            throw new DomainException("This order is not fully picked, so it cannot be packed.");
        }

        if (PackClaimedBy is not null && !string.Equals(PackClaimedBy, staff, StringComparison.Ordinal))
        {
            return PackClaimedBy;
        }

        PackClaimedBy = staff;
        StatusId = FulfilmentStatuses.Packing;
        return null;
    }

    /// <summary>A second, independent scan check (PAK-1): each unit counted separately from the pick, never past the quantity (PAK-2).</summary>
    public ScanResult ScanPack(string staff, string gtin)
    {
        if (StatusId != FulfilmentStatuses.Packing)
        {
            return new ScanResult(false, "NOT_PACKING", "This order is not open at the pack station.");
        }

        if (!string.Equals(PackClaimedBy, staff, StringComparison.Ordinal))
        {
            return new ScanResult(false, "NOT_YOUR_PACK", $"{PackClaimedBy} holds this parcel.");
        }

        var code = Normalise(gtin);
        var line = _lines.FirstOrDefault(l => l.IsActive && l.Gtin == code);
        if (line is null)
        {
            return new ScanResult(false, "GTIN_NOT_ON_ORDER", "This item is not on the order. Take it out of the parcel.");
        }

        if (line.PackedQuantity >= line.Quantity)
        {
            return new ScanResult(false, "SURPLUS_UNIT", "There is one unit too many of this item. Take it out of the parcel.");
        }

        line.PackedQuantity++;
        if (line.PackedQuantity == line.Quantity)
        {
            line.StatusId = LineStatuses.Packed;
        }

        return new ScanResult(true, "UNIT_OK", $"{line.Name}, {line.Size}: {line.PackedQuantity} of {line.Quantity}.");
    }

    /// <summary>The lines that still lack units at the pack station (PAK-3).</summary>
    public IReadOnlyList<string> MissingAtPack() =>
        [.. _lines.Where(l => l.IsActive && l.PackedQuantity < l.Quantity).Select(l => l.Sku ?? l.Name)];

    /// <summary>Stores the weighed parcel against the expected weight (PAK-5 to PAK-8).</summary>
    public WeightCheckResult RecordWeight(string staff, int grams, int expectedGrams, short packagingKind, int tolerancePercent, int toleranceGrams, DateTimeOffset at)
    {
        EnsureStaff(staff);
        if (StatusId != FulfilmentStatuses.Packing || !string.Equals(PackClaimedBy, staff, StringComparison.Ordinal))
        {
            throw new DomainException("Open the order at the pack station first.");
        }

        var check = WeightCheck.Evaluate(grams, expectedGrams, tolerancePercent, toleranceGrams);
        if (!check.IsValid)
        {
            throw new DomainException(check.Problem!);
        }

        if (Pack is null)
        {
            Pack = new PackRecord(grams, expectedGrams, packagingKind, !check.IsWithinTolerance, staff, at);
        }
        else
        {
            Pack.Reweigh(grams, expectedGrams, packagingKind, !check.IsWithinTolerance, staff, at);
        }

        return check;
    }

    /// <summary>
    /// Seals the parcel. Needs every unit scanned, a recorded weight and, when the weight is off, a note that flags it for staff (PAK-3, PAK-6, PAK-7).
    /// Returns false for a repeat: one packed state and one event (PAK-13).
    /// </summary>
    public bool CompletePack(string staff, string? note, DateTimeOffset at)
    {
        if (StatusId == FulfilmentStatuses.Packed)
        {
            return false;
        }

        EnsureStaff(staff);
        if (StatusId != FulfilmentStatuses.Packing || !string.Equals(PackClaimedBy, staff, StringComparison.Ordinal))
        {
            throw new DomainException("Open the order at the pack station first.");
        }

        var missing = MissingAtPack();
        if (missing.Count > 0)
        {
            throw new DomainException("Units are missing from the parcel: " + string.Join(", ", missing) + ".");
        }

        if (Pack is null)
        {
            throw new DomainException("Weigh the parcel first.");
        }

        if (Pack.IsDeviation)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new DomainException("The weight does not fit the order. Check the parcel, or add a note to complete anyway.");
            }

            Pack.AddNote(note);
        }

        StatusId = FulfilmentStatuses.Packed;
        PackedAt = at;
        PackedBy = staff;
        Raise(new OrderPackedEvent(OrderId, OrderNumber, Pack.WeightGrams));
        return true;
    }

    /// <summary>The slip needs printing only once the order is at or past the pack station.</summary>
    public bool CanPrintSlip => StatusId is FulfilmentStatuses.Packing or FulfilmentStatuses.Packed;

    public void RecordSlipPrinted(DateTimeOffset at)
    {
        SlipPrintCount++;
        SlipPrintedAt = at;
        SlipPrintPending = false;
    }

    public void MarkSlipPrintPending() => SlipPrintPending = true;

    private static string Normalise(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static void EnsureStaff(string staff)
    {
        if (string.IsNullOrWhiteSpace(staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }
    }
}
