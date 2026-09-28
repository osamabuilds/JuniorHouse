using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>Written to the outbox in the same transaction as each PO status change (ADR 0004, AC-10/AC-11/AC-12).</summary>
public sealed record PoCreatedEvent(long PoId, string PoNo, long VendorId, long StyleId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record PoSentToVendorEvent(long PoId, string PoNo) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record PoAcknowledgedEvent(long PoId, string PoNo) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record PoCancelledEvent(long PoId, string PoNo, short CancelReasonId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

/// <summary>SCRUM-93 task 22 (plan.md's Events section). Minimal v1 shape - task 44 finalises the full envelope/snapshot payload additively, same as Sprint 1's own events extend v1-&gt;v2.</summary>
public sealed record PoRevisionProposedEvent(long PoId, string PoNo, short RevisionNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record PoRevisionPutInForceEvent(long PoId, string PoNo, short RevisionNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record PoRevisionSupersededEvent(long PoId, string PoNo, short RevisionNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
