using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Infrastructure.ProductionRuns;

// ADR 0008: PROD consumes VNDR's PO events through the shared dispatcher. Each consumer is idempotent:
// the inbox row (keyed by the outbox message id and this handler) is added to PROD's own DbContext and commits in the
// same SaveChanges as the effect, so a redelivered message is a no-op (AC-2) and a failed one leaves nothing behind.

/// <summary>PurchaseOrder acknowledged (BRD 9.10.3): open the production run (AC-1).</summary>
internal sealed class PoAcknowledgedConsumer(ISender sender, ProductionDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<PoAcknowledgedEvent>
{
    public async Task HandleAsync(PoAcknowledgedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "OpenRunOnPoAcknowledged", cancellationToken))
        {
            await sender.Send(new OpenProductionRunCommand(domainEvent.PoId), cancellationToken);
        }
    }
}

/// <summary>The PO was cancelled: its run closes as cancelled (AC-29).</summary>
internal sealed class PoCancelledConsumer(ISender sender, ProductionDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<PoCancelledEvent>
{
    public async Task HandleAsync(PoCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CancelRunOnPoCancelled", cancellationToken))
        {
            await sender.Send(new MarkRunCancelledCommand(domainEvent.PoId), cancellationToken);
        }
    }
}

/// <summary>The PO was closed manually (D2): its run closes with it.</summary>
internal sealed class PoClosedConsumer(ISender sender, ProductionDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<PoClosedEvent>
{
    public async Task HandleAsync(PoClosedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CloseRunOnPoClosed", cancellationToken))
        {
            await sender.Send(new MarkRunClosedCommand(domainEvent.PoId), cancellationToken);
        }
    }
}

/// <summary>A revision came into force: refresh the latest-acceptable-date copy and the re-sample flags (AC-16, AC-27).</summary>
internal sealed class PoRevisionPutInForceConsumer(ISender sender, ProductionDbContext dbContext, InboxGuard guard)
    : IOutboxMessageHandler<PoRevisionPutInForceEvent>
{
    public async Task HandleAsync(PoRevisionPutInForceEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "RefreshRunOnRevisionInForce", cancellationToken))
        {
            await sender.Send(
                new RefreshRunTermsCommand(domainEvent.PoId, domainEvent.Terms?.LatestAcceptableDate, domainEvent.IsConstructionChange),
                cancellationToken);
        }
    }
}
