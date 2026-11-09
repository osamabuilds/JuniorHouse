using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Application.Allocations;
using Romp.Modules.Fulfilment.Application.PickExceptions;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.PickExceptions;

/// <summary>S14 F9: a picker flags a stock mismatch instead of force-completing; staff resolve it (PEX-1 to PEX-10).</summary>
[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class PickExceptionTests(FulfilmentPostgres postgres)
{
    private static async Task<(FulfilmentHost Host, long Id, (long OrderId, string OrderNumber, long[] VariantIds, string[] Gtins) Placed)> PickingAsync(
        FulfilmentPostgres postgres, params (int Quantity, string BinCode, int OnShelf)[] lines)
    {
        var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, lines.Length == 0 ? [(3, "A-01-1", 10), (1, "B-02-2", 10)] : lines);
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.ClaimAsync(host, id);
        return (host, id, placed);
    }

    private static Task<ExceptionDto?> RaiseAsync(FulfilmentHost host, long id, long variantId, string bin = "A-01-1", string reason = "SHORT", int found = 0, string staff = FulfilmentHost.Picker) =>
        host.SendAsync(new RaiseExceptionCommand(id, staff, variantId, bin, reason, found));

    [Fact]
    [Trait("Spec", "PEX-1")]
    public async Task AShortShelf_MarksTheLineAwaitingRestock_AndTheUnitsAlreadyScannedStayCounted()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        await Flow.ScanAsync(host, id, "A-01-1");
        await Flow.ScanAsync(host, id, placed.Gtins[0]);

        var raised = await RaiseAsync(host, id, placed.VariantIds[0], found: 0);

        Assert.NotNull(raised);
        Assert.Equal((2, 0), (raised.SystemCount, raised.FoundCount));
        var line = (await Flow.LoadAsync(postgres, id)).Lines.Single(l => l.VariantId == placed.VariantIds[0]);
        Assert.Equal(LineStatuses.AwaitingRestock, line.StatusId);
        Assert.Equal(1, line.PickedQuantity);
        Assert.Equal(1, line.Allocations.Single().Quantity);
        Assert.Equal((placed.VariantIds[0], 2), (host.Allocator.Shortened.Single().VariantId, host.Allocator.Shortened.Single().Quantity));
    }

    [Fact]
    [Trait("Spec", "PEX-2")]
    public async Task AfterAnException_TheOtherLinesCanStillBePicked_ButTheOrderCannotCompleteYet()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        await RaiseAsync(host, id, placed.VariantIds[0]);

        Assert.True((await Flow.ScanAsync(host, id, "B-02-2")).Accepted);
        Assert.True((await Flow.ScanAsync(host, id, placed.Gtins[1])).Accepted);
        var late = await Flow.ScanAsync(host, id, placed.Gtins[0]);

        Assert.False(late.Accepted);
        Assert.Equal("LINE_ON_HOLD", late.Code);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker)));
    }

    [Fact]
    [Trait("Spec", "PEX-3")]
    public async Task RaisingOne_PublishesTheDelayedLineEvent_WithIdsAndCodesOnly()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;

        await RaiseAsync(host, id, placed.VariantIds[0], reason: "DAMAGED");

        var delayed = await Flow.EventsAsync(postgres, "OrderLineDelayedEvent", placed.OrderNumber);
        var payload = Assert.Single(delayed);
        Assert.Contains("DAMAGED", payload);
        Assert.DoesNotContain("Sara", payload);
        Assert.DoesNotContain("Lahore", payload);
        Assert.Single(await Flow.EventsAsync(postgres, "PickExceptionRaisedEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PEX-4")]
    public async Task TheExceptionQueue_ShowsOrderSkuBinCountsPickerAndTime_Paged()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        host.Time.Now = FulfilmentHost.Start.AddMinutes(3);
        await RaiseAsync(host, id, placed.VariantIds[0], found: 1);

        var queue = await host.SendAsync(new ListExceptionsQuery(true, 1, 100));

        var row = Assert.Single(queue.Items, e => e.OrderId == id);
        Assert.Equal((placed.OrderNumber, $"SKU-{placed.VariantIds[0]}", "A-01-1", "SHORT", "OPEN"), (row.OrderNumber, row.Sku, row.BinCode, row.Reason, row.Status));
        Assert.Equal((3, 1, FulfilmentHost.Picker, FulfilmentHost.Start.AddMinutes(3)), (row.SystemCount, row.FoundCount, row.RaisedBy, row.RaisedAt));
    }

    [Theory]
    [Trait("Spec", "PEX-5")]
    [InlineData("", 0)]
    [InlineData("LOST", 0)]
    [InlineData("SHORT", -1)]
    [InlineData("SHORT", 4)]
    public async Task AReasonIsRequired_AndTheFoundCountIsBetweenZeroAndTheAllocatedQuantity(string reason, int found)
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;

        await Assert.ThrowsAsync<DomainException>(() => RaiseAsync(host, id, placed.VariantIds[0], reason: reason, found: found));
        Assert.Equal(LineStatuses.Allocated, (await Flow.LoadAsync(postgres, id)).Lines.Single(l => l.VariantId == placed.VariantIds[0]).StatusId);
    }

    [Fact]
    [Trait("Spec", "PEX-6")]
    public async Task AnException_NeverOverwritesStock_ItGivesUpTheClaimAndLeavesTheBinFlagToWarehouse()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        var stockBefore = host.Allocator.Stock[placed.VariantIds[0]].Bins.Single().Quantity;

        await RaiseAsync(host, id, placed.VariantIds[0]);

        Assert.Equal(stockBefore, host.Allocator.Stock[placed.VariantIds[0]].Bins.Single().Quantity);
        Assert.Empty(host.Allocator.Confirmed);
        var raised = Assert.Single(await Flow.EventsAsync(postgres, "PickExceptionRaisedEvent", placed.OrderNumber));
        Assert.Contains("A-01-1", raised);
    }

    [Fact]
    [Trait("Spec", "PEX-7")]
    public async Task WhenTheLastLineIsFlagged_TheOrderIsHeld_AndLeavesThePickQueue()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        await RaiseAsync(host, id, placed.VariantIds[0]);
        await RaiseAsync(host, id, placed.VariantIds[1], bin: "B-02-2");

        Assert.Equal(FulfilmentStatuses.Held, (await Flow.LoadAsync(postgres, id)).StatusId);
        var queue = await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 100));
        Assert.DoesNotContain(queue.Items, i => i.OrderId == id);
        var held = await host.SendAsync(new ListQueueQuery(QueueStage.Held, 1, 100));
        Assert.Contains(held.Items, i => i.OrderId == id);
    }

    [Fact]
    [Trait("Spec", "PEX-8")]
    public async Task WhenStockArrives_ResolvingAllocatesTheLineAgain_AndTheOrderReturnsToTheQueue()
    {
        var (host, id, placed) = await PickingAsync(postgres, (3, "A-01-1", 10));
        using var _ = host;
        await RaiseAsync(host, id, placed.VariantIds[0]);
        var exceptionId = (await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items.Single(e => e.OrderId == id).Id;
        Assert.Equal(FulfilmentStatuses.Held, (await Flow.LoadAsync(postgres, id)).StatusId);

        var resolved = await host.SendAsync(new ResolveExceptionCommand(exceptionId, ResolveAction.Restocked, "Manager"));

        Assert.Equal(("RESOLVED", "Manager"), (resolved!.Status, resolved.ResolvedBy));
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(FulfilmentStatuses.Queued, order.StatusId);
        Assert.Equal(LineStatuses.Allocated, order.Lines.Single().StatusId);
        Assert.Equal(3, order.Lines.Single().Allocations.Sum(a => a.Quantity));
        Assert.Contains(host.Allocator.Cleared, b => b == placed.VariantIds[0] * 10);
        Assert.DoesNotContain((await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items, e => e.OrderId == id);
    }

    [Fact]
    [Trait("Spec", "PEX-8")]
    public async Task ResolvingAsRestocked_WhenTheBinsAreStillShort_KeepsTheExceptionOpen()
    {
        var (host, id, placed) = await PickingAsync(postgres, (3, "A-01-1", 10));
        using var _ = host;
        await RaiseAsync(host, id, placed.VariantIds[0], found: 0);
        host.Allocator.Stock[placed.VariantIds[0]].Bins[0] = (placed.VariantIds[0] * 10, "A-01-1", FulfilmentHost.WalkKey("A-01-1"), 0);
        var exceptionId = (await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items.Single(e => e.OrderId == id).Id;

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ResolveExceptionCommand(exceptionId, ResolveAction.Restocked, "Manager")));

        Assert.Single((await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items, e => e.OrderId == id);
    }

    [Fact]
    [Trait("Spec", "PEX-9")]
    public async Task DroppingTheDelayedLine_RemovesItFromPicking_AndTheRestOfTheOrderCanBePacked()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        await RaiseAsync(host, id, placed.VariantIds[0]);
        var exceptionId = (await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items.Single(e => e.OrderId == id).Id;

        await host.SendAsync(new ResolveExceptionCommand(exceptionId, ResolveAction.CancelLine, "Manager"));
        await Flow.ScanAsync(host, id, "B-02-2");
        await Flow.ScanAsync(host, id, placed.Gtins[1]);
        var completed = await host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker));

        Assert.True(completed);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(LineStatuses.Cancelled, order.Lines.Single(l => l.VariantId == placed.VariantIds[0]).StatusId);
        Assert.Equal(FulfilmentStatuses.Picked, order.StatusId);
        var picks = Assert.Single(host.Allocator.Confirmed).Picks;
        Assert.Equal(placed.VariantIds[1], Assert.Single(picks).VariantId);
    }

    [Fact]
    [Trait("Spec", "PEX-9")]
    public async Task ALineWithUnitsAlreadyPicked_CannotBeDropped()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        await Flow.ScanAsync(host, id, "A-01-1");
        await Flow.ScanAsync(host, id, placed.Gtins[0]);
        await RaiseAsync(host, id, placed.VariantIds[0]);
        var exceptionId = (await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items.Single(e => e.OrderId == id).Id;

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ResolveExceptionCommand(exceptionId, ResolveAction.CancelLine, "Manager")));
    }

    [Fact]
    [Trait("Spec", "PEX-10")]
    public async Task EveryException_RecordsTheStaffMemberAndTheTime_OfRaisingAndResolving()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;
        host.Time.Now = FulfilmentHost.Start.AddMinutes(10);
        await RaiseAsync(host, id, placed.VariantIds[0]);
        var exceptionId = (await host.SendAsync(new ListExceptionsQuery(true, 1, 100))).Items.Single(e => e.OrderId == id).Id;
        host.Time.Now = FulfilmentHost.Start.AddHours(2);
        await host.SendAsync(new ResolveExceptionCommand(exceptionId, ResolveAction.CancelLine, "Manager One"));

        await using var db = postgres.NewContext();
        var row = await db.Exceptions.SingleAsync(e => e.Id == exceptionId);
        Assert.Equal((FulfilmentHost.Picker, FulfilmentHost.Start.AddMinutes(10)), (row.RaisedBy, row.RaisedAt));
        Assert.Equal(("Manager One", FulfilmentHost.Start.AddHours(2)), (row.ResolvedBy, row.ResolvedAt));
        Assert.False(row.WasRestocked);
    }

    [Fact]
    [Trait("Spec", "PEX-4")]
    public async Task AnotherPickerCannotRaiseAnExceptionOnAnOrderTheyDoNotHold()
    {
        var (host, id, placed) = await PickingAsync(postgres);
        using var _ = host;

        await Assert.ThrowsAsync<DomainException>(() => RaiseAsync(host, id, placed.VariantIds[0], staff: FulfilmentHost.OtherPicker));
        Assert.Null(await RaiseAsync(host, 404_404_404, placed.VariantIds[0]));
        Assert.NotNull(JsonSerializer.Serialize(await host.SendAsync(new ListExceptionsQuery(false, 1, 5))));
    }
}
