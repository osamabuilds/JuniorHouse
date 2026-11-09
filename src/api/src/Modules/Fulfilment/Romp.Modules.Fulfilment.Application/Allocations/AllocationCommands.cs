using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Fulfilment.Application.Allocations;

public enum CreateOutcome
{
    Created,

    /// <summary>A fulfilment order already exists for this order: nothing changes (ALC-5, ALC-11).</summary>
    AlreadyExists,

    /// <summary>Orders knows no such order, or it was cancelled before it reached the queue.</summary>
    NotEligible,
}

/// <summary>
/// An order was confirmed (cash on delivery) or paid (online): create its fulfilment order and allocate its lines to bins (ALC-1, ALC-2). What to
/// ship is read from Orders at this moment, never from the event (ADR 0026). A repeat does nothing (ALC-5, ALC-11).
/// </summary>
public sealed record CreateFulfilmentOrderCommand(long OrderId) : IRequest<CreateOutcome>, IFulfilmentCommand;

public sealed class CreateFulfilmentOrderCommandHandler(
    IFulfilmentDbContext dbContext, IOrderFulfilmentQueries orders, IStockAllocator allocator, TimeProvider clock)
    : IRequestHandler<CreateFulfilmentOrderCommand, CreateOutcome>
{
    public async Task<CreateOutcome> Handle(CreateFulfilmentOrderCommand request, CancellationToken cancellationToken)
    {
        if (await dbContext.Orders.AsNoTracking().AnyAsync(o => o.OrderId == request.OrderId, cancellationToken))
        {
            return CreateOutcome.AlreadyExists;
        }

        var view = await orders.FindAsync(request.OrderId, cancellationToken);
        if (view is null || view.StatusCode == "CNCL")
        {
            return CreateOutcome.NotEligible;
        }

        var seeds = view.Lines.Select(l => new FulfilmentLineSeed(l.VariantId, l.Sku, l.Name, l.Colour, l.Size, l.Quantity, l.UnitPrice)).ToList();
        var order = FulfilmentOrder.Create(view.OrderId, view.OrderNumber, view.PlacementKey, view.IsCashOnDelivery, view.CashToCollect, clock.GetUtcNow(), seeds);

        // Warehouse does the allocation in its own transaction; it is idempotent on the key, so a retry after a failed save is safe (ALC-5).
        var result = await allocator.AllocateAsync(view.PlacementKey, AllocationRequests.For(order), cancellationToken);
        order.ApplyAllocation(AllocationRequests.Seeds(result));

        dbContext.Orders.Add(order);
        return CreateOutcome.Created;
    }
}

/// <summary>An order was cancelled: release its allocation before pick, or mark a put-back task after some units were scanned (ALC-6, ALC-7).</summary>
public sealed record CancelFulfilmentOrderCommand(long OrderId) : IRequest<bool>, IFulfilmentCommand;

public sealed class CancelFulfilmentOrderCommandHandler(IFulfilmentDbContext dbContext, IStockAllocator allocator)
    : IRequestHandler<CancelFulfilmentOrderCommand, bool>
{
    public async Task<bool> Handle(CancelFulfilmentOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.Lines).ThenInclude(l => l.Allocations).FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);
        if (order is null || !order.CancelFromOrder())
        {
            return false;
        }

        // Nothing was decremented yet, so the claimed units simply go back to the pool (ALC-6).
        await allocator.ReleaseAsync(order.PlacementKey, cancellationToken);
        return true;
    }
}

/// <summary>Maps between the aggregate and Warehouse's allocation contract in one place.</summary>
public static class AllocationRequests
{
    public static IReadOnlyList<AllocationLineRequest> For(FulfilmentOrder order) =>
        [.. order.ActiveLines.Select(l => new AllocationLineRequest(l.VariantId, l.Quantity))];

    public static IReadOnlyList<AllocationSeed> Seeds(AllocationResult result) =>
        [.. result.Allocations.Select(a => new AllocationSeed(a.VariantId, a.BinId, a.BinCode, a.WalkKey, a.Quantity, a.Gtin, a.Sku))];
}
