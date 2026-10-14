using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;

namespace Romp.BuildingBlocks.Persistence.Inbox;

/// <summary>
/// Makes an outbox consumer idempotent (ADR 0004, ADR 0008). A consumer calls
/// <see cref="TryBeginAsync"/> first: false means this exact message was already handled by this
/// handler, so it does nothing; true adds the <see cref="InboxMessage"/> row to the module's own
/// DbContext, which then commits in the same SaveChanges (transaction) as the handler's effect.
/// </summary>
public sealed class InboxGuard(OutboxMessageContext messageContext, TimeProvider timeProvider)
{
    public async Task<bool> TryBeginAsync(DbContext dbContext, string handlerName, CancellationToken cancellationToken)
    {
        var messageId = messageContext.MessageId;
        var key = $"{messageContext.Schema}/{handlerName}";

        var alreadyHandled = await dbContext.Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == messageId && m.HandlerName == key, cancellationToken);
        if (alreadyHandled)
        {
            return false;
        }

        dbContext.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = messageId,
            HandlerName = key,
            ProcessedDte = timeProvider.GetUtcNow(),
        });
        return true;
    }
}
