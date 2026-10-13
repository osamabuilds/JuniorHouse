using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Infrastructure.Persistence;
using Romp.Modules.Quality.Contracts;

namespace Romp.Modules.Labelling.Infrastructure.LabelWork;

/// <summary>
/// Labelling consumes QC's <c>QcBatchAccepted</c> through the shared dispatcher (ADR 0004). The inbox row is added to Labelling's own
/// DbContext and commits in the same SaveChanges as the work items, so a redelivered message is a no-op (LW-2) and a failed one
/// leaves nothing behind. A message the mapper refuses is never stored and never takes a GTIN number (LW-6): the consumer throws, the
/// dispatcher retries and then dead-letters it, keeping the payload and the reason for inspection.
/// </summary>
internal sealed partial class QcBatchAcceptedConsumer(
    ISender sender, SkuDbContext dbContext, InboxGuard guard, IOptions<LabellingOptions> options, ILogger<QcBatchAcceptedConsumer> logger)
    : IOutboxMessageHandler<QcBatchAcceptedEvent>
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Refused QcBatchAccepted (sub-batch {SubBatchId}): {Reason}")]
    private partial void LogRefused(long subBatchId, string reason);

    public async Task HandleAsync(QcBatchAcceptedEvent domainEvent, CancellationToken cancellationToken)
    {
        var mapped = QcBatchAcceptedMapper.Map(domainEvent, options.Value.MaxLinesPerAcceptedBatch);
        if (!mapped.IsValid)
        {
            LogRefused(domainEvent.SubBatchId, mapped.Rejection ?? string.Empty);
            throw new InvalidOperationException($"QcBatchAccepted refused: {mapped.Rejection}");
        }

        if (await guard.TryBeginAsync(dbContext, "RecordLabelWorkOnQcBatchAccepted", cancellationToken))
        {
            await sender.Send(new RecordAcceptedBatchCommand(mapped.Batch!), cancellationToken);
        }
    }
}
