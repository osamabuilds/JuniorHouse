using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Application.Allocations;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.Allocations;

/// <summary>S14 F3 and F4: a confirmed or paid order enters the queue with its lines allocated to bins; a cancel releases or raises a put-back (ALC-1 to ALC-11).</summary>
[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class AllocationTests(FulfilmentPostgres postgres)
{
    [Fact]
    [Trait("Spec", "ALC-1")]
    public async Task AConfirmedCashOnDeliveryOrder_BecomesOneQueuedFulfilmentOrder_WithEveryLineInABin()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(cod: true, (2, "A-01-1", 10), (1, "B-02-3", 5));

        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(FulfilmentStatuses.Queued, order.StatusId);
        Assert.True(order.IsCashOnDelivery);
        Assert.Equal(placed.OrderNumber, order.OrderNumber);
        Assert.All(order.Lines, l => Assert.Equal(LineStatuses.Allocated, l.StatusId));
        Assert.Equal(["A-01-1", "B-02-3"], order.Lines.SelectMany(l => l.Allocations).Select(a => a.BinCode).Order());
    }

    [Fact]
    [Trait("Spec", "ALC-2")]
    public async Task APaidOnlineOrder_AlsoEntersTheQueue_WithNothingToCollect()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(cod: false, (1, "A-01-1", 4));

        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        var order = await Flow.LoadAsync(postgres, id);
        Assert.False(order.IsCashOnDelivery);
        Assert.Equal(0m, order.CashToCollect);
        Assert.Equal(FulfilmentStatuses.Queued, order.StatusId);
    }

    [Fact]
    [Trait("Spec", "ALC-4")]
    public async Task ALineSplitAcrossTwoBins_ShowsBothLocations()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (4, "A-01-1", 2));
        host.Allocator.Stock[placed.VariantIds[0]].Bins.Add((9_999_001, "C-05-2", FulfilmentHost.WalkKey("C-05-2"), 10));

        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        var allocations = (await Flow.LoadAsync(postgres, id)).Lines.Single().Allocations;
        Assert.Equal([("A-01-1", 2), ("C-05-2", 2)], allocations.OrderBy(a => a.WalkKey, StringComparer.Ordinal).Select(a => (a.BinCode, a.Quantity)));
    }

    [Fact]
    [Trait("Spec", "ALC-5")]
    [Trait("Spec", "ALC-11")]
    public async Task TheSameEventTwice_LeavesOneFulfilmentOrder_AndAskingWarehouseOnce()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 4));

        await host.SendAsync(new CreateFulfilmentOrderCommand(placed.OrderId));
        var second = await host.SendAsync(new CreateFulfilmentOrderCommand(placed.OrderId));

        Assert.Equal(CreateOutcome.AlreadyExists, second);
        Assert.Equal(1, host.Allocator.AllocateCalls);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.OrderId == placed.OrderId));
    }

    [Fact]
    [Trait("Spec", "ALC-11")]
    public async Task AnOrderAlreadyPicked_IsNotChangedByASecondConfirmation()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 4));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.PickAndCompleteAsync(host, postgres, id);

        var again = await host.SendAsync(new CreateFulfilmentOrderCommand(placed.OrderId));

        Assert.Equal(CreateOutcome.AlreadyExists, again);
        Assert.Equal(FulfilmentStatuses.Picked, (await Flow.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "ALC-8")]
    public async Task AShortLine_WaitsForRestock_TheOtherLinesStillQueue_AndTheCustomerIsTold()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (3, "A-01-1", 1), (1, "B-02-3", 5));

        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(FulfilmentStatuses.Queued, order.StatusId);
        Assert.Equal(LineStatuses.AwaitingRestock, order.Lines.Single(l => l.VariantId == placed.VariantIds[0]).StatusId);
        Assert.Equal(LineStatuses.Allocated, order.Lines.Single(l => l.VariantId == placed.VariantIds[1]).StatusId);
        var delayed = await Flow.EventsAsync(postgres, "OrderLineDelayedEvent", placed.OrderNumber);
        Assert.Single(delayed);
        Assert.Contains("NO_STOCK", delayed[0]);
    }

    [Fact]
    [Trait("Spec", "PEX-7")]
    public async Task EveryLineShort_HoldsTheOrder_AndItLeavesThePickQueue()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (3, "A-01-1", 0));

        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        Assert.Equal(FulfilmentStatuses.Held, (await Flow.LoadAsync(postgres, id)).StatusId);
        var queue = await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 100));
        Assert.DoesNotContain(queue.Items, i => i.OrderId == id);
    }

    [Fact]
    [Trait("Spec", "ALC-8")]
    public async Task AnOrderTheOrdersModuleDoesNotKnowOrHasCancelled_IsNotEligible()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 4));
        host.Orders.Views[placed.OrderId] = host.Orders.Views[placed.OrderId] with { StatusCode = "CNCL" };

        Assert.Equal(CreateOutcome.NotEligible, await host.SendAsync(new CreateFulfilmentOrderCommand(placed.OrderId)));
        Assert.Equal(CreateOutcome.NotEligible, await host.SendAsync(new CreateFulfilmentOrderCommand(404_404_404)));
    }

    [Fact]
    [Trait("Spec", "ALC-6")]
    public async Task CancelledBeforeAnyPick_ReleasesTheAllocation_AndClosesThePickList()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 4));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        var cancelled = await host.SendAsync(new CancelFulfilmentOrderCommand(placed.OrderId));

        Assert.True(cancelled);
        Assert.Equal([$"key-{placed.OrderId}"], host.Allocator.Released);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(FulfilmentStatuses.Cancelled, order.StatusId);
        Assert.False(order.PutBackRequired);
        var queue = await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 100));
        Assert.DoesNotContain(queue.Items, i => i.OrderId == id);
    }

    [Fact]
    [Trait("Spec", "ALC-6")]
    public async Task CancellingTwice_ReleasesOnce()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 4));
        await Seed.CreateAsync(host, placed.OrderId, postgres);

        await host.SendAsync(new CancelFulfilmentOrderCommand(placed.OrderId));
        var second = await host.SendAsync(new CancelFulfilmentOrderCommand(placed.OrderId));

        Assert.False(second);
        Assert.Single(host.Allocator.Released);
    }

    [Fact]
    [Trait("Spec", "ALC-7")]
    [Trait("Spec", "PCK-12")]
    public async Task CancelledMidPick_ListsThePickedUnitsForPutBack_AndTheNextScanIsRefused()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (2, "A-01-1", 4));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");
        await Flow.ScanAsync(host, id, placed.Gtins[0]);

        await host.SendAsync(new CancelFulfilmentOrderCommand(placed.OrderId));

        var order = await Flow.LoadAsync(postgres, id);
        Assert.True(order.PutBackRequired);
        var refused = await Flow.ScanAsync(host, id, placed.Gtins[0]);
        Assert.False(refused.Accepted);
        Assert.Equal("ORDER_CANCELLED", refused.Code);
        Assert.Contains("put", refused.Message, StringComparison.OrdinalIgnoreCase);
        var putBack = await host.SendAsync(new ListQueueQuery(QueueStage.PutBack, 1, 100));
        Assert.Contains(putBack.Items, i => i.OrderId == id);
    }

    [Fact]
    [Trait("Spec", "ALC-7")]
    public async Task CancelledAfterThePickWasCompleted_IsLeftToTheLaterStages()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 4));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.PickAndCompleteAsync(host, postgres, id);

        var cancelled = await host.SendAsync(new CancelFulfilmentOrderCommand(placed.OrderId));

        Assert.False(cancelled);
        Assert.Empty(host.Allocator.Released);
        Assert.Equal(FulfilmentStatuses.Picked, (await Flow.LoadAsync(postgres, id)).StatusId);
    }
}
