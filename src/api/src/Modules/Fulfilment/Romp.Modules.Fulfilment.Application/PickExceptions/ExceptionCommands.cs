using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Application.Allocations;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Fulfilment.Application.PickExceptions;

public sealed record ExceptionDto(long Id, long OrderId, string OrderNumber, string? Sku, string BinCode, string Reason, string Status, int SystemCount, int FoundCount, string RaisedBy, DateTimeOffset RaisedAt, string? ResolvedBy, DateTimeOffset? ResolvedAt);

/// <summary>
/// The picker says the shelf does not hold what the system says (PEX-1, FR-SC-11). The line waits for restock, the units already scanned stay
/// counted, the other lines can still be picked (PEX-2), and the customer is told through an event (PEX-3). Stock is never overwritten (PEX-6): the
/// claim in the bin is given up and Warehouse flags the bin so it is counted before it is used again.
/// </summary>
public sealed record RaiseExceptionCommand(long OrderId, string Staff, long VariantId, string BinCode, string Reason, int FoundCount) : IRequest<ExceptionDto?>, IFulfilmentCommand;

public sealed class RaiseExceptionCommandHandler(IFulfilmentDbContext dbContext, IStockAllocator allocator, TimeProvider clock) : IRequestHandler<RaiseExceptionCommand, ExceptionDto?>
{
    public async Task<ExceptionDto?> Handle(RaiseExceptionCommand request, CancellationToken cancellationToken)
    {
        var reason = ExceptionReasons.FromCode(request.Reason) ?? throw new DomainException("Choose a reason: not found, short or damaged.");
        var order = await dbContext.Orders.Include(o => o.Lines).ThenInclude(l => l.Allocations).Include(o => o.Exceptions).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var (exception, givenUp, binId) = order.RaiseException(request.Staff, request.VariantId, ScanCodes.Clean(request.BinCode), reason, request.FoundCount, clock.GetUtcNow());
        await allocator.ShortenClaimAsync(order.PlacementKey, request.VariantId, binId, givenUp, cancellationToken);
        var sku = order.Lines.FirstOrDefault(l => l.VariantId == request.VariantId)?.Sku;
        return new ExceptionDto(exception.Id, order.Id, order.OrderNumber, sku, exception.BinCode, ExceptionReasons.CodeOf(exception.ReasonId), "OPEN", exception.SystemQuantity, exception.FoundQuantity, exception.RaisedBy, exception.RaisedAt, null, null);
    }
}

/// <summary>The exception queue for staff (PEX-4): order, SKU, bin, system count, count found, picker and time. Paged, oldest first.</summary>
public sealed record ListExceptionsQuery(bool OpenOnly = true, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<ExceptionDto>>;

public sealed class ListExceptionsQueryHandler(IFulfilmentDbContext dbContext) : IRequestHandler<ListExceptionsQuery, PagedResult<ExceptionDto>>
{
    public async Task<PagedResult<ExceptionDto>> Handle(ListExceptionsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Exceptions.AsNoTracking();
        if (request.OpenOnly)
        {
            query = query.Where(e => e.StatusId == ExceptionStatuses.Open);
        }

        // One join to the order and one to the line for the SKU: a constant number of queries however many exceptions there are.
        var page = await (
                from e in query
                join o in dbContext.Orders.AsNoTracking() on e.OrderId equals o.Id
                join l in dbContext.Lines.AsNoTracking() on new { OrderId = o.Id, e.VariantId } equals new { l.OrderId, l.VariantId }
                orderby e.RaisedAt, e.Id
                select new { e.Id, OrderId = o.Id, o.OrderNumber, l.Sku, e.BinCode, e.ReasonId, e.StatusId, e.SystemQuantity, e.FoundQuantity, e.RaisedBy, e.RaisedAt, e.ResolvedBy, e.ResolvedAt })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        return new PagedResult<ExceptionDto>(
            [.. page.Items.Select(e => new ExceptionDto(e.Id, e.OrderId, e.OrderNumber, e.Sku, e.BinCode, ExceptionReasons.CodeOf(e.ReasonId), e.StatusId == ExceptionStatuses.Open ? "OPEN" : "RESOLVED", e.SystemQuantity, e.FoundQuantity, e.RaisedBy, e.RaisedAt, e.ResolvedBy, e.ResolvedAt))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>How staff close an exception.</summary>
public enum ResolveAction
{
    /// <summary>Stock arrived (or was counted): the line is allocated again and the order returns to the pick queue (PEX-8).</summary>
    Restocked,

    /// <summary>The customer would rather drop the delayed line: it leaves picking (PEX-9).</summary>
    CancelLine,
}

public sealed record ResolveExceptionCommand(long ExceptionId, ResolveAction Action, string Staff) : IRequest<ExceptionDto?>, IFulfilmentCommand;

public sealed class ResolveExceptionCommandHandler(IFulfilmentDbContext dbContext, IStockAllocator allocator, TimeProvider clock) : IRequestHandler<ResolveExceptionCommand, ExceptionDto?>
{
    public async Task<ExceptionDto?> Handle(ResolveExceptionCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.Lines).ThenInclude(l => l.Allocations).Include(o => o.Exceptions)
            .FirstOrDefaultAsync(o => o.Exceptions.Any(e => e.Id == request.ExceptionId), cancellationToken);
        if (order is null)
        {
            return null;
        }

        var exception = order.Exceptions.First(e => e.Id == request.ExceptionId);
        var now = clock.GetUtcNow();
        if (request.Action == ResolveAction.CancelLine)
        {
            // The line leaves picking, so whatever Warehouse still holds for it goes back to the pool.
            var held = order.Lines.Where(l => l.VariantId == exception.VariantId).SelectMany(l => l.Allocations).Select(a => (a.BinId, a.Quantity)).ToList();
            order.ResolveCancelLine(exception.Id, request.Staff, now);
            foreach (var (binId, quantity) in held)
            {
                await allocator.ShortenClaimAsync(order.PlacementKey, exception.VariantId, binId, quantity, cancellationToken);
            }
        }
        else
        {
            // Staff say the shelf is right again: clear the review flag, allocate what was short, and only then close the exception. If the bins still
            // cannot cover the line, the exception stays open and the answer says so (the allocation is idempotent, so asking again later is safe).
            await allocator.ClearBinFlagAsync(exception.BinId, cancellationToken);
            var result = await allocator.AllocateAsync(order.PlacementKey, AllocationRequests.For(order), cancellationToken);
            if (result.Shortages.Any(s => s.VariantId == exception.VariantId))
            {
                var shortage = result.Shortages.First(s => s.VariantId == exception.VariantId);
                throw new DomainException($"Still short: {shortage.Allocated} of {shortage.Requested} units are in stock.");
            }

            order.ResolveRestocked(exception.Id, request.Staff, now);
            order.ApplyAllocation(AllocationRequests.Seeds(result));
            order.AfterRestock();
        }

        var sku = order.Lines.FirstOrDefault(l => l.VariantId == exception.VariantId)?.Sku;
        return new ExceptionDto(exception.Id, order.Id, order.OrderNumber, sku, exception.BinCode, ExceptionReasons.CodeOf(exception.ReasonId), "RESOLVED", exception.SystemQuantity, exception.FoundQuantity, exception.RaisedBy, exception.RaisedAt, request.Staff, now);
    }
}
