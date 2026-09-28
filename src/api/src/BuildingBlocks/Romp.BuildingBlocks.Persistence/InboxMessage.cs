namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Convention for a module's own idempotency bookkeeping table, applied once it registers a handler
/// with a database effect (SCRUM-93 task 12; plan.md's Data section: "&lt;SCHEMA&gt;.INBX"). Not
/// instantiated in any real schema this sprint - VNDR's placeholder logging handler (task 13) has no
/// DB effect, so it needs no inbox row. Documented now so Sprint 3's first real consumer doesn't have
/// to redesign it (ADR 0004, refined by ADR 0007).
/// </summary>
public sealed class InboxMessage
{
    /// <summary>The outbox message id this row confirms as handled - not a foreign key across schemas (ADR 0002): each consuming module records its own copy.</summary>
    public required long MessageId { get; init; }

    /// <summary>Distinguishes handlers when more than one module handler consumes the same message.</summary>
    public required string HandlerName { get; init; }

    public DateTimeOffset? ProcessedDte { get; init; }
}
