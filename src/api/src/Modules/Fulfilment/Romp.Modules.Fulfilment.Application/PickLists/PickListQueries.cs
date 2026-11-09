using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.Allocations;

namespace Romp.Modules.Fulfilment.Application.PickLists;

/// <summary>One order in the pick or pack queue. Order number only: no customer data (PCK-11).</summary>
public sealed record QueueItemDto(
    long OrderId, string OrderNumber, string Status, DateTimeOffset QueuedAt, int AgeMinutes, int LineCount, int UnitCount, string? ClaimedBy, bool PutBackRequired, bool IsCashOnDelivery);

/// <summary>Which part of the flow the queue shows.</summary>
public enum QueueStage
{
    /// <summary>Orders waiting to be picked, or being picked.</summary>
    Pick,

    /// <summary>Orders picked and waiting to be packed, or being packed.</summary>
    Pack,

    /// <summary>Orders held because every line waits for restock.</summary>
    Held,

    /// <summary>Cancelled orders whose scanned units must be put back.</summary>
    PutBack,
}

/// <summary>The pick queue, oldest first, paged (APP-8, PCK-1). One count and one page query however many orders there are.</summary>
public sealed record ListQueueQuery(QueueStage Stage = QueueStage.Pick, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<QueueItemDto>>;

public sealed class ListQueueQueryHandler(IFulfilmentDbContext dbContext, TimeProvider clock) : IRequestHandler<ListQueueQuery, PagedResult<QueueItemDto>>
{
    public async Task<PagedResult<QueueItemDto>> Handle(ListQueueQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Orders.AsNoTracking();
        query = request.Stage switch
        {
            QueueStage.Pick => query.Where(o => o.StatusId == FulfilmentStatuses.Queued || o.StatusId == FulfilmentStatuses.Picking),
            QueueStage.Pack => query.Where(o => o.StatusId == FulfilmentStatuses.Picked || o.StatusId == FulfilmentStatuses.Packing),
            QueueStage.Held => query.Where(o => o.StatusId == FulfilmentStatuses.Held),
            _ => query.Where(o => o.StatusId == FulfilmentStatuses.Cancelled && o.PutBackRequired),
        };

        var page = await query
            .OrderBy(o => o.QueuedAt).ThenBy(o => o.Id)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.StatusId,
                o.QueuedAt,
                Lines = o.Lines.Count(l => l.StatusId != LineStatuses.Cancelled),
                Units = o.Lines.Where(l => l.StatusId != LineStatuses.Cancelled).Sum(l => (int?)l.Quantity) ?? 0,
                ClaimedBy = o.StatusId == FulfilmentStatuses.Packing ? o.PackClaimedBy : o.PickClaimedBy,
                o.PutBackRequired,
                o.IsCashOnDelivery,
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var now = clock.GetUtcNow();
        return new PagedResult<QueueItemDto>(
            [.. page.Items.Select(o => new QueueItemDto(o.Id, o.OrderNumber, FulfilmentStatuses.CodeOf(o.StatusId), o.QueuedAt, (int)Math.Max(0, (now - o.QueuedAt).TotalMinutes), o.Lines, o.Units, o.ClaimedBy, o.PutBackRequired, o.IsCashOnDelivery))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One shelf stop on a pick list: a line's units in one bin. A line waiting for restock has no bin.</summary>
public sealed record PickRowDto(long LineId, long VariantId, string BinCode, string? Sku, string Name, string Colour, string Size, int Quantity, int Picked, string Status);

/// <summary>An order's pick list in walking order (PCK-1). It shows the order number only: no name, address or phone (PCK-11).</summary>
public sealed record PickListDto(
    long OrderId, string OrderNumber, string Status, string? ClaimedBy, string? CurrentBin, string? NextBin, bool ReadyToComplete, bool PutBackRequired, IReadOnlyList<PickRowDto> Rows);

public sealed record GetPickListQuery(long OrderId) : IRequest<PickListDto?>;

public sealed class GetPickListQueryHandler(IFulfilmentDbContext dbContext) : IRequestHandler<GetPickListQuery, PickListDto?>
{
    public async Task<PickListDto?> Handle(GetPickListQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.Lines).ThenInclude(l => l.Allocations)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        return order is null ? null : PickListMapper.Map(order);
    }
}

internal static class PickListMapper
{
    public static PickListDto Map(FulfilmentOrder order)
    {
        var rows = new List<(string Key, PickRowDto Row)>();
        foreach (var line in order.Lines.Where(l => l.IsActive))
        {
            if (line.Allocations.Count == 0)
            {
                rows.Add(("~", new PickRowDto(line.Id, line.VariantId, string.Empty, line.Sku, line.Name, line.Colour, line.Size, line.Quantity, line.PickedQuantity, LineStatuses.CodeOf(line.StatusId))));
                continue;
            }

            foreach (var allocation in line.Allocations)
            {
                rows.Add((allocation.WalkKey, new PickRowDto(line.Id, line.VariantId, allocation.BinCode, line.Sku, line.Name, line.Colour, line.Size, allocation.Quantity, allocation.PickedQuantity, LineStatuses.CodeOf(line.StatusId))));
            }
        }

        return new PickListDto(
            order.Id, order.OrderNumber, FulfilmentStatuses.CodeOf(order.StatusId), order.PickClaimedBy, order.CurrentBin, order.NextBin(),
            order.IsReadyToComplete, order.PutBackRequired,
            [.. rows.OrderBy(r => r.Key, StringComparer.Ordinal).ThenBy(r => r.Row.LineId).Select(r => r.Row)]);
    }
}
