using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>One size/colour line in an event's terms snapshot.</summary>
public sealed record PoTermsLine(short SizeId, short ColourId, int Qty);

/// <summary>
/// SCRUM-93 task 44/45 (AC-49, AC-50): the commercial terms an event reports, in full, so a
/// consumer never has to call back to learn them. Contains only what the vendor also sees - there
/// is no field here for the internal impact note, vendor evidence or internal files, so no event
/// payload can carry them (AC-50).
/// </summary>
public sealed record PoTermsSnapshot(
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    short PaymentTermId,
    decimal AdvancePercent,
    short? FabricResponsibilityId,
    IReadOnlyList<PoTermsLine> Lines)
{
    public static PoTermsSnapshot From(PurchaseOrderRevision revision) => new(
        revision.UnitCost, revision.ExpectedDeliveryDate, revision.LatestAcceptableDate,
        revision.OverTolerancePercent, revision.UnderTolerancePercent, revision.PaymentTermId,
        revision.AdvancePercent, revision.FabricResponsibilityId,
        revision.Lines.Select(l => new PoTermsLine(l.SizeId, l.ColourId, l.Qty)).ToList());
}

/// <summary>
/// The shared envelope of every VNDR event (AC-48): message id (<see cref="EventId"/>), event type,
/// schema version, UTC occurrence time, aggregate type/id and <c>PoNo</c>. Written to the outbox in
/// the same transaction as the change that raised it (ADR 0004).
/// </summary>
public abstract record PoEvent(long PoId, string PoNo) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public string AggregateType => "PurchaseOrder";

    public long AggregateId => PoId;

    public abstract short SchemaVersion { get; }
}

/// <summary>Schema v2 (AC-48): v1's fields unchanged, plus the terms snapshot. No revision number - a Draft has none.</summary>
public sealed record PoCreatedEvent(long PoId, string PoNo, long VendorId, long StyleId, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 2;
}

/// <summary>Schema v2: v1's fields unchanged, plus the in-force revision number and the terms it put in force.</summary>
public sealed record PoSentToVendorEvent(long PoId, string PoNo, short? RevisionNumber = null, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 2;
}

public sealed record PoAcknowledgedEvent(long PoId, string PoNo, short? RevisionNumber = null, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 2;
}

public sealed record PoCancelledEvent(long PoId, string PoNo, short CancelReasonId, short? RevisionNumber = null, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 2;
}

/// <summary>Schema v1 (new in Sprint 2): a revision was proposed and is awaiting a decision. Terms are the proposal's.</summary>
public sealed record PoRevisionProposedEvent(long PoId, string PoNo, short RevisionNumber, short InitiatorId = 0, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

public sealed record PoRevisionPutInForceEvent(long PoId, string PoNo, short RevisionNumber, short InitiatorId = 0, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

public sealed record PoRevisionSupersededEvent(long PoId, string PoNo, short RevisionNumber, short InitiatorId = 0, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

/// <summary>SCRUM-93 task 24.</summary>
public sealed record PoRevisionRejectedEvent(long PoId, string PoNo, short RevisionNumber, short InitiatorId = 0, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

/// <summary>SCRUM-93 task 25 (also raised by CancelPurchaseOrderCommand's auto-withdraw, task 26, AC-21).</summary>
public sealed record PoRevisionWithdrawnEvent(long PoId, string PoNo, short RevisionNumber, short InitiatorId = 0, PoTermsSnapshot? Terms = null) : PoEvent(PoId, PoNo)
{
    public override short SchemaVersion => 1;
}
