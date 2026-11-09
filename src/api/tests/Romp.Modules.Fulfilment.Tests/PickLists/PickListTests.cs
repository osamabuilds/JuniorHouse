using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.PickLists;

/// <summary>S14 F5 to F8 and F14: the queue and the walking-order pick list, claiming, scanning with refusals, completing, and the scan log (PCK-1 to PCK-12).</summary>
[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class PickListTests(FulfilmentPostgres postgres)
{
    private static async Task<(FulfilmentHost Host, long Id, (long OrderId, string OrderNumber, long[] VariantIds, string[] Gtins) Placed)> QueuedAsync(
        FulfilmentPostgres postgres, params (int Quantity, string BinCode, int OnShelf)[] lines)
    {
        var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, lines.Length == 0 ? [(2, "A-01-1", 10)] : lines);
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        return (host, id, placed);
    }

    // ---------------------------------------------------------------- the queue and the list (PCK-1, PCK-11, APP-8)

    [Fact]
    [Trait("Spec", "PCK-1")]
    public async Task ThePickList_IsInWalkingOrder_WithBinSkuSizeColourAndQuantity()
    {
        var (host, id, _) = await QueuedAsync(postgres, (1, "C-10-1", 5), (1, "A-02-3", 5), (2, "A-10-1", 5));
        using var _ = host;

        var list = (await host.SendAsync(new GetPickListQuery(id)))!;

        Assert.Equal(["A-02-3", "A-10-1", "C-10-1"], list.Rows.Select(r => r.BinCode));
        var first = list.Rows[0];
        Assert.Equal(("Floral Dress", "Red", "2-3Y", 1), (first.Name, first.Colour, first.Size, first.Quantity));
        Assert.False(string.IsNullOrEmpty(first.Sku));
    }

    [Fact]
    [Trait("Spec", "PCK-11")]
    public async Task ThePickListAndTheQueue_ShowTheOrderNumberOnly()
    {
        var (host, id, placed) = await QueuedAsync(postgres);
        using var _ = host;

        var list = await host.SendAsync(new GetPickListQuery(id));
        var queue = await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 100));

        var json = JsonSerializer.Serialize(list) + JsonSerializer.Serialize(queue.Items.Where(i => i.OrderId == id));
        Assert.Contains(placed.OrderNumber, json);
        Assert.DoesNotContain("Sara", json);
        Assert.DoesNotContain("Lahore", json);
    }

    [Fact]
    [Trait("Spec", "APP-8")]
    public async Task TheQueue_IsPagedOldestFirst_WithTheAge_InAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new FulfilmentHost(postgres, counter: counter);
        var ids = new List<long>();
        for (var i = 0; i < 4; i++)
        {
            host.Time.Now = FulfilmentHost.Start.AddMinutes(i);
            ids.Add(await Seed.CreateAsync(host, host.PlaceOrder(true, (1, "A-01-1", 5)).OrderId, postgres));
        }

        host.Time.Now = FulfilmentHost.Start.AddMinutes(60);
        counter.Reset();
        var page = await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 4));
        var small = counter.Count;
        counter.Reset();
        await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 100));

        Assert.Equal(2, small);
        Assert.Equal(small, counter.Count);
        Assert.True(page.Items.Zip(page.Items.Skip(1)).All(p => p.First.QueuedAt <= p.Second.QueuedAt));
        Assert.All(page.Items, i => Assert.True(i.AgeMinutes >= 0));
        var newest = await host.SendAsync(new ListQueueQuery(QueueStage.Pick, 1, 1));
        Assert.Single(newest.Items);
        Assert.True(newest.TotalCount >= 4);
    }

    // ---------------------------------------------------------------- claiming (PCK-9)

    [Fact]
    [Trait("Spec", "PCK-9")]
    public async Task TwoPickersOpenTheSameOrder_OneHoldsIt_TheOtherIsToldWho()
    {
        var (host, id, _) = await QueuedAsync(postgres);
        using var _ = host;

        var first = await Flow.ClaimAsync(host, id, FulfilmentHost.Picker);
        var second = await Flow.ClaimAsync(host, id, FulfilmentHost.OtherPicker);
        var scan = await Flow.ScanAsync(host, id, "A-01-1", FulfilmentHost.OtherPicker);

        Assert.True(first.Claimed);
        Assert.False(second.Claimed);
        Assert.Equal(FulfilmentHost.Picker, second.HeldBy);
        Assert.False(scan.Accepted);
        Assert.Equal("NOT_YOUR_PICK", scan.Code);
        Assert.Contains(FulfilmentHost.Picker, scan.Message);
    }

    [Fact]
    [Trait("Spec", "PCK-9")]
    public async Task TheHolderCanLetGo_AndAnotherPickerThenTakesIt_ButOnlyAManagerCanTakeItOver()
    {
        var (host, id, _) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.ClaimAsync(host, id);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ReleasePickClaimCommand(id, FulfilmentHost.OtherPicker, false)));
        Assert.True(await host.SendAsync(new ReleasePickClaimCommand(id, FulfilmentHost.Picker, false)));

        Assert.True((await Flow.ClaimAsync(host, id, FulfilmentHost.OtherPicker)).Claimed);
        Assert.True(await host.SendAsync(new ReleasePickClaimCommand(id, "Manager", true)));
        Assert.Equal(FulfilmentStatuses.Queued, (await Flow.LoadAsync(postgres, id)).StatusId);
    }

    // ---------------------------------------------------------------- scanning (PCK-2 to PCK-6, PCK-10)

    [Fact]
    [Trait("Spec", "PCK-2")]
    public async Task TheRightBinThenTheRightBarcode_CountsTheUnit()
    {
        var (host, id, placed) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.ClaimAsync(host, id);

        var bin = await Flow.ScanAsync(host, id, "a-01-1 ");
        var unit = await Flow.ScanAsync(host, id, placed.Gtins[0]);

        Assert.True(bin.Accepted);
        Assert.Equal("A-01-1", bin.CurrentBin);
        Assert.True(unit.Accepted);
        Assert.Equal((1, 2), (unit.PickedUnits, unit.TotalUnits));
        Assert.Equal(1, (await Flow.LoadAsync(postgres, id)).Lines.Single().PickedQuantity);
    }

    [Fact]
    [Trait("Spec", "PCK-3")]
    public async Task ABarcodeNotOnTheOrder_IsRefusedInWords_AndNothingIsCounted()
    {
        var (host, id, _) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");

        var refused = await Flow.ScanAsync(host, id, "2999999999990");

        Assert.False(refused.Accepted);
        Assert.Equal("GTIN_NOT_ON_ORDER", refused.Code);
        Assert.Equal(0, (await Flow.LoadAsync(postgres, id)).Lines.Single().PickedQuantity);
    }

    [Fact]
    [Trait("Spec", "PCK-3")]
    public async Task ABarcodeBeforeAnyShelfWasScanned_IsRefused_WithTheNextBin()
    {
        var (host, id, placed) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.ClaimAsync(host, id);

        var refused = await Flow.ScanAsync(host, id, placed.Gtins[0]);

        Assert.False(refused.Accepted);
        Assert.Equal("SCAN_BIN_FIRST", refused.Code);
        Assert.Equal("A-01-1", refused.NextBin);
    }

    [Fact]
    [Trait("Spec", "PCK-4")]
    public async Task AShelfOtherThanTheLines_IsRefused_AndThePickerIsToldWhereTheLineIs()
    {
        var (host, id, placed) = await QueuedAsync(postgres, (1, "A-01-1", 5), (1, "B-02-2", 5));
        using var _ = host;
        await Flow.ClaimAsync(host, id);

        var refused = await Flow.ScanAsync(host, id, "Z-09-9");
        await Flow.ScanAsync(host, id, "B-02-2");
        var wrongBin = await Flow.ScanAsync(host, id, placed.Gtins[0]);

        Assert.False(refused.Accepted);
        Assert.Equal("BIN_NOT_ON_LIST", refused.Code);
        Assert.Equal("A-01-1", refused.NextBin);
        Assert.False(wrongBin.Accepted);
        Assert.Equal("WRONG_BIN", wrongBin.Code);
        Assert.Contains("A-01-1", wrongBin.Message);
    }

    [Fact]
    [Trait("Spec", "PCK-5")]
    public async Task ALineOfThree_IsDoneAfterThreeScans_AndAFourthIsRefused()
    {
        var (host, id, placed) = await QueuedAsync(postgres, (3, "A-01-1", 10));
        using var _ = host;
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await Flow.ScanAsync(host, id, placed.Gtins[0])).Accepted);
        }

        var fourth = await Flow.ScanAsync(host, id, placed.Gtins[0]);
        Assert.False(fourth.Accepted);
        Assert.Equal("OVER_PICK", fourth.Code);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(3, order.Lines.Single().PickedQuantity);
        Assert.Equal(LineStatuses.Picked, order.Lines.Single().StatusId);
        Assert.True(fourth.ReadyToComplete);
    }

    [Fact]
    [Trait("Spec", "PCK-6")]
    public async Task AScan_AnswersInUnder300Milliseconds_WithAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new FulfilmentHost(postgres, counter: counter);
        var placed = host.PlaceOrder(true, (30, "A-01-1", 100));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");
        await Flow.ScanAsync(host, id, placed.Gtins[0]);

        var times = new List<double>();
        var queries = new List<int>();
        for (var i = 0; i < 20; i++)
        {
            counter.Reset();
            var watch = Stopwatch.StartNew();
            await Flow.ScanAsync(host, id, placed.Gtins[0]);
            times.Add(watch.Elapsed.TotalMilliseconds);
            queries.Add(counter.Count);
        }

        Assert.True(times.Order().ElementAt(18) < 300, $"p95 was {times.Order().ElementAt(18):0} ms");
        Assert.All(queries, q => Assert.True(q <= 3, $"a scan ran {q} reader queries"));
    }

    [Fact]
    [Trait("Spec", "PCK-10")]
    public async Task EveryScan_IsLoggedWithTheStaffMemberTheTimeAndTheOutcome()
    {
        var (host, id, placed) = await QueuedAsync(postgres);
        using var _ = host;
        host.Time.Now = FulfilmentHost.Start.AddMinutes(5);
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");
        await Flow.ScanAsync(host, id, placed.Gtins[0]);
        await Flow.ScanAsync(host, id, "2999999999990");

        await using var db = postgres.NewContext();
        var log = await db.Scans.Where(s => s.OrderId == placed.OrderId).OrderBy(s => s.Id).ToListAsync();
        Assert.Equal([ScanOutcomes.Accepted, ScanOutcomes.Accepted, ScanOutcomes.Refused], log.Select(s => s.OutcomeId));
        Assert.All(log, s => Assert.Equal(FulfilmentHost.Picker, s.Staff));
        Assert.All(log, s => Assert.Equal(FulfilmentHost.Start.AddMinutes(5), s.At));
        Assert.All(log, s => Assert.Equal(ScanStages.Pick, s.StageId));
    }

    [Theory]
    [Trait("Spec", "SEC-25")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A-01-1'; DROP TABLE \"FULF\".\"FULF_ORDR\";--")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task HostileScanInput_IsRejectedBeforeItReachesAQueryOrTheLog(string code)
    {
        var (host, id, _) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.ClaimAsync(host, id);

        await Assert.ThrowsAsync<DomainException>(() => Flow.ScanAsync(host, id, code));
    }

    // ---------------------------------------------------------------- completing (PCK-7, PCK-8)

    [Fact]
    [Trait("Spec", "PCK-7")]
    public async Task CompletingThePick_HandsEveryAllocatedUnitToWarehouseOnce_AndMovesOnToPacking()
    {
        var (host, id, placed) = await QueuedAsync(postgres, (2, "A-01-1", 10), (1, "B-02-2", 5));
        using var _ = host;
        await Flow.PickEverythingAsync(host, postgres, id);

        var completed = await host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker));

        Assert.True(completed);
        var confirmed = Assert.Single(host.Allocator.Confirmed);
        Assert.Equal($"key-{placed.OrderId}", confirmed.Key);
        Assert.Equal(FulfilmentHost.Picker, confirmed.Staff);
        Assert.Equal(3, confirmed.Picks.Sum(p => p.Quantity));
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(FulfilmentStatuses.Picked, order.StatusId);
        Assert.Equal(FulfilmentHost.Picker, order.PickedBy);
        Assert.Single(await Flow.EventsAsync(postgres, "PickCompletedEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PCK-8")]
    public async Task CompletingTwice_DecrementsOnce_AndAScanAfterwardsIsRefused()
    {
        var (host, id, placed) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.PickEverythingAsync(host, postgres, id);

        var first = await host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker));
        var second = await host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker));
        var scan = await Flow.ScanAsync(host, id, placed.Gtins[0]);

        Assert.True(first);
        Assert.False(second);
        Assert.Single(host.Allocator.Confirmed);
        Assert.Single(await Flow.EventsAsync(postgres, "PickCompletedEvent", placed.OrderNumber));
        Assert.False(scan.Accepted);
    }

    [Fact]
    [Trait("Spec", "PCK-7")]
    public async Task CompletingBeforeEveryUnitIsScanned_IsRefused()
    {
        var (host, id, placed) = await QueuedAsync(postgres);
        using var _ = host;
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");
        await Flow.ScanAsync(host, id, placed.Gtins[0]);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker)));
        Assert.Empty(host.Allocator.Confirmed);
    }

    // ---------------------------------------------------------------- the scan log purge (F14)

    [Fact]
    [Trait("Spec", "NFR-SC-05")]
    public async Task ThePurge_DeletesOnlyScansOlderThanTheRetention()
    {
        using var host = new FulfilmentHost(postgres, o => o.ScanLogRetentionDays = 30);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 5));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        host.Time.Now = FulfilmentHost.Start.AddDays(-45);
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");
        host.Time.Now = FulfilmentHost.Start.AddDays(-5);
        await Flow.ScanAsync(host, id, "B-09-9");
        host.Time.Now = FulfilmentHost.Start;

        var deleted = await host.SendAsync(new PurgeScanLogCommand());

        Assert.True(deleted >= 1);
        await using var db = postgres.NewContext();
        var left = await db.Scans.Where(s => s.OrderId == placed.OrderId).ToListAsync();
        Assert.Equal(FulfilmentHost.Start.AddDays(-5), Assert.Single(left).At);
    }

    [Fact]
    [Trait("Spec", "APP-1")]
    public async Task AnUnknownOrder_IsAnAnswerNotAnError()
    {
        using var host = new FulfilmentHost(postgres);

        Assert.Null(await host.SendAsync(new GetPickListQuery(404_404_404)));
        Assert.True((await host.SendAsync(new ClaimPickCommand(404_404_404, FulfilmentHost.Picker))).NotFound);
        Assert.Null(await host.SendAsync(new ScanPickCommand(404_404_404, FulfilmentHost.Picker, "A-01-1")));
        Assert.Null(await host.SendAsync(new CompletePickCommand(404_404_404, FulfilmentHost.Picker)));
        _ = host.Scope().ServiceProvider.GetRequiredService<TimeProvider>();
    }
}
