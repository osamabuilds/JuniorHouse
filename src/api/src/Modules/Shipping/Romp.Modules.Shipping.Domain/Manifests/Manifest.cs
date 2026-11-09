using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Shipping.Domain.Manifests;

/// <summary>One parcel on a manifest (SHPG.MNFS_LINE) and whether it was handed over.</summary>
public sealed class ManifestLine : Entity<long>, IAuditable
{
    private ManifestLine()
    {
        InsrBy = string.Empty;
    }

    internal ManifestLine(long shipmentId)
    {
        ShipmentId = shipmentId;
        HandedOver = true;
        InsrBy = string.Empty;
    }

    public long ManifestId { get; private set; }

    public long ShipmentId { get; private set; }

    /// <summary>Counts as handed to the courier unless staff say otherwise after a mismatch (MNF-6).</summary>
    public bool HandedOver { get; internal set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// The end-of-day list of parcels handed to one courier on one business day (SHPG.MNFS_MAIN, FR-SC-15), counted against the parcels the courier
/// actually took. Open until counted; Matched when the numbers agree; Mismatch when they do not, until staff mark what was not handed over and
/// close it; then frozen (MNF-7).
/// </summary>
public sealed class Manifest : AggregateRoot<long>, IAuditable
{
    public const int MaxNoteLength = 200;

    private readonly List<ManifestLine> _lines = [];

    private Manifest()
    {
        InsrBy = string.Empty;
    }

    public short CourierId { get; private set; }

    public DateOnly BusinessDate { get; private set; }

    public short StatusId { get; private set; } = ManifestStatuses.Open;

    public int? CountedParcels { get; private set; }

    public string? CountedBy { get; private set; }

    public DateTimeOffset? CountedAt { get; private set; }

    public string? Note { get; private set; }

    public IReadOnlyList<ManifestLine> Lines => _lines;

    public bool IsFrozen => StatusId is ManifestStatuses.Matched or ManifestStatuses.Closed;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static Manifest Open(short courierId, DateOnly businessDate) => new() { CourierId = courierId, BusinessDate = businessDate };

    /// <summary>Adds a parcel. The same parcel twice is the same line (MNF-4). A counted manifest takes no more (MNF-7).</summary>
    public bool Add(long shipmentId)
    {
        if (StatusId != ManifestStatuses.Open)
        {
            throw new DomainException("This manifest has been counted, so no parcel can be added.");
        }

        if (_lines.Any(l => l.ShipmentId == shipmentId))
        {
            return false;
        }

        _lines.Add(new ManifestLine(shipmentId));
        return true;
    }

    /// <summary>Staff entered the number of parcels the courier took. Equal is Matched and frozen; different is a Mismatch to resolve (MNF-5).</summary>
    public void Count(int parcels, string staff, DateTimeOffset at)
    {
        EnsureStaff(staff);
        if (StatusId != ManifestStatuses.Open)
        {
            throw new DomainException("This manifest has already been counted.");
        }

        if (parcels < 0 || parcels > 10_000)
        {
            throw new DomainException("The parcel count must be a whole number from 0 to 10000.");
        }

        CountedParcels = parcels;
        CountedBy = staff;
        CountedAt = at;
        StatusId = parcels == _lines.Count ? ManifestStatuses.Matched : ManifestStatuses.Mismatch;
    }

    /// <summary>After a mismatch, staff say which parcels the courier did take (MNF-6).</summary>
    public void MarkHandedOver(long shipmentId, bool handedOver)
    {
        if (StatusId != ManifestStatuses.Mismatch)
        {
            throw new DomainException("Parcels are marked only on a manifest with a mismatch.");
        }

        var line = _lines.FirstOrDefault(l => l.ShipmentId == shipmentId) ?? throw new DomainException("That parcel is not on this manifest.");
        line.HandedOver = handedOver;
    }

    /// <summary>Closes a mismatch. Returns the parcels not handed over: they go back to be put on the next manifest (MNF-6, MNF-7).</summary>
    public IReadOnlyList<long> Resolve(string note, string staff)
    {
        EnsureStaff(staff);
        if (StatusId != ManifestStatuses.Mismatch)
        {
            throw new DomainException("Only a manifest with a mismatch needs resolving.");
        }

        var text = (note ?? string.Empty).Trim();
        if (text.Length is 0 or > MaxNoteLength)
        {
            throw new DomainException($"Say what happened in 1 to {MaxNoteLength} characters.");
        }

        Note = text;
        StatusId = ManifestStatuses.Closed;
        return [.. _lines.Where(l => !l.HandedOver).Select(l => l.ShipmentId)];
    }

    private static void EnsureStaff(string staff)
    {
        if (string.IsNullOrWhiteSpace(staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }
    }
}
