namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>
/// Scoped, filled in by <see cref="OutboxDispatcher"/> just before it invokes a handler, so the
/// handler (and the inbox guard) can tell which outbox row it is processing. The row id, unlike
/// the payload's event id, is stable across redeliveries - that is what makes a consumer idempotent.
/// </summary>
public sealed class OutboxMessageContext
{
    public long MessageId { get; set; }

    public string Schema { get; set; } = string.Empty;
}
