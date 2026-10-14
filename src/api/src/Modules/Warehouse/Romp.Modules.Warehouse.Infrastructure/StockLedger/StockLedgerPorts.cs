using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using MediatR;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.StockLedger;

/// <summary>Stock-ledger's public read surface (plan): warehouse's own variant reference plus on-hand totals. One indexed query per call.</summary>
public sealed class StockVariantReader(IWarehouseDbContext dbContext) : IStockVariantReader
{
    public Task<StockVariantInfo?> FindByGtinAsync(string gtin, CancellationToken cancellationToken) =>
        Find(v => v.Gtin == gtin, cancellationToken);

    public Task<StockVariantInfo?> FindByVariantIdAsync(long variantId, CancellationToken cancellationToken) =>
        Find(v => v.VariantId == variantId, cancellationToken);

    public Task<StockVariantInfo?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        Find(v => v.Id == id, cancellationToken);

    public Task<StockVariantInfo?> FindByStyleColourSizeAsync(long styleId, short colourId, short sizeId, CancellationToken cancellationToken) =>
        Find(v => v.StyleId == styleId && v.ColourId == colourId && v.SizeId == sizeId, cancellationToken);

    public async Task<int> GetOnHandAsync(long variantRefId, CancellationToken cancellationToken) =>
        await dbContext.Balances.AsNoTracking().Where(b => b.VariantRefId == variantRefId).SumAsync(b => b.Quantity, cancellationToken);

    public async Task<bool> HasEverHeldStockAsync(long variantRefId, CancellationToken cancellationToken) =>
        await dbContext.Movements.AsNoTracking().AnyAsync(m => m.VariantRefId == variantRefId, cancellationToken);

    private async Task<StockVariantInfo?> Find(System.Linq.Expressions.Expression<Func<Domain.StockLedger.VariantRef, bool>> predicate, CancellationToken cancellationToken) =>
        await dbContext.VariantRefs.AsNoTracking().Where(predicate)
            .Select(v => new StockVariantInfo(v.Id, v.VariantId, v.StyleId, v.ColourId, v.SizeId, v.SkuCode, v.Gtin, v.GtinRetired))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>Implements bins' <see cref="IBinStockQuery"/>: one grouped query answers for any number of bins.</summary>
public sealed class BinStockQuery(IWarehouseDbContext dbContext) : IBinStockQuery
{
    public async Task<IReadOnlyDictionary<long, int>> GetUnitsOnHandAsync(IReadOnlyCollection<long> binIds, CancellationToken cancellationToken) =>
        await dbContext.Balances.AsNoTracking()
            .Where(b => binIds.Contains(b.BinId))
            .GroupBy(b => b.BinId)
            .Select(g => new { BinId = g.Key, Units = g.Sum(b => b.Quantity) })
            .ToDictionaryAsync(x => x.BinId, x => x.Units, cancellationToken);
}

/// <summary>
/// Warehouse consumes Labelling's <c>VariantGtinAssigned</c> through the shared dispatcher (ADR 0004). The inbox row commits with the
/// reference, so a redelivered message is a no-op. A message the mapper refuses is never stored: the consumer throws, the dispatcher
/// retries and then dead-letters it with the reason.
/// </summary>
internal sealed partial class VariantGtinAssignedConsumer(
    ISender sender, WhseDbContext dbContext, InboxGuard guard, ILogger<VariantGtinAssignedConsumer> logger)
    : IOutboxMessageHandler<VariantGtinAssignedEvent>
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Refused VariantGtinAssigned (variant {VariantId}): {Reason}")]
    private partial void LogRefused(long variantId, string reason);

    public async Task HandleAsync(VariantGtinAssignedEvent domainEvent, CancellationToken cancellationToken)
    {
        var (command, rejection) = VariantEventMapper.Map(domainEvent);
        if (command is null)
        {
            LogRefused(domainEvent.VariantId, rejection ?? string.Empty);
            throw new InvalidOperationException($"VariantGtinAssigned refused: {rejection}");
        }

        if (await guard.TryBeginAsync(dbContext, "RecordVariantOnGtinAssigned", cancellationToken))
        {
            await sender.Send(command, cancellationToken);
        }
    }
}

/// <summary>Warehouse consumes Labelling's <c>GtinVoided</c> the same way (D3: a retired GTIN is refused at putaway).</summary>
internal sealed partial class GtinVoidedConsumer(
    ISender sender, WhseDbContext dbContext, InboxGuard guard, ILogger<GtinVoidedConsumer> logger)
    : IOutboxMessageHandler<GtinVoidedEvent>
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Refused GtinVoided (variant {VariantId}): {Reason}")]
    private partial void LogRefused(long variantId, string reason);

    public async Task HandleAsync(GtinVoidedEvent domainEvent, CancellationToken cancellationToken)
    {
        var (command, rejection) = VariantEventMapper.Map(domainEvent);
        if (command is null)
        {
            LogRefused(domainEvent.VariantId, rejection ?? string.Empty);
            throw new InvalidOperationException($"GtinVoided refused: {rejection}");
        }

        if (await guard.TryBeginAsync(dbContext, "RecordVariantOnGtinVoided", cancellationToken))
        {
            await sender.Send(command, cancellationToken);
        }
    }
}
