using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Warehouse.Application.Allocations;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Allocations;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Allocations;

/// <summary>W1 to W4: allocation to bins oldest first, idempotent top-up and release, the pick decrement, and bins under review (ALC-3 to 10, PCK-7, 8, PEX-6).</summary>
[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class StockAllocatorTests(WarehousePostgres postgres)
{
    private static string NewKey() => $"aloc-{Guid.NewGuid():N}";

    private static async Task PutAsync(WarehouseHost host, long refId, long binId, int units) =>
        await host.SendAsync(new TestPostCommand([new StockPost(refId, Grades.Accepted, binId, units, MovementKinds.Putaway, 77)]));

    private static Task<AllocationResult> AllocateAsync(WarehouseHost host, string key, params (long VariantId, int Quantity)[] lines) =>
        host.InScopeAsync(sp => sp.GetRequiredService<IStockAllocator>().AllocateAsync(key, [.. lines.Select(l => new AllocationLineRequest(l.VariantId, l.Quantity))], CancellationToken.None));

    private static async Task<int> OnHandAsync(WarehousePostgres postgres, long refId)
    {
        await using var db = postgres.NewContext();
        return await db.Balances.Where(b => b.VariantRefId == refId).SumAsync(b => b.Quantity);
    }

    [Fact]
    [Trait("Spec", "ALC-3")]
    public async Task TwoBinsWithDifferentPutawayDates_TheOlderBinIsUsedFirst()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var newer = await host.NewBinAsync();
        var older = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, older.Id, 5);
        host.Time.Now = host.Time.Now.AddDays(3);
        await PutAsync(host, variant.RefId, newer.Id, 5);

        var result = await AllocateAsync(host, NewKey(), (variant.VariantId, 2));

        var unit = Assert.Single(result.Allocations);
        Assert.Equal(older.Id, unit.BinId);
        Assert.Equal(2, unit.Quantity);
        Assert.Equal(variant.Gtin, unit.Gtin);
        Assert.Empty(result.Shortages);
    }

    [Fact]
    [Trait("Spec", "ALC-4")]
    public async Task TheOldestBinIsTooSmall_TheRestComesFromTheNextOldest_AndBothLocationsAreListed()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var first = await host.NewBinAsync();
        var second = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, first.Id, 2);
        host.Time.Now = host.Time.Now.AddDays(1);
        await PutAsync(host, variant.RefId, second.Id, 5);

        var result = await AllocateAsync(host, NewKey(), (variant.VariantId, 4));

        Assert.Equal([(first.Id, 2), (second.Id, 2)], result.Allocations.OrderBy(a => a.BinId == first.Id ? 0 : 1).Select(a => (a.BinId, a.Quantity)));
        Assert.Empty(result.Shortages);
    }

    [Fact]
    [Trait("Spec", "ALC-3")]
    public async Task ABinEmptiedAndRefilled_CountsFromTheRefill_NotTheOldPutaway()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var refilled = await host.NewBinAsync();
        var steady = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, refilled.Id, 3);
        host.Time.Now = host.Time.Now.AddDays(1);
        await PutAsync(host, variant.RefId, steady.Id, 3);
        host.Time.Now = host.Time.Now.AddDays(1);
        await host.SendAsync(new TestPostCommand([new StockPost(variant.RefId, Grades.Accepted, refilled.Id, -3, MovementKinds.Adjustment, 78)]));
        host.Time.Now = host.Time.Now.AddDays(1);
        await PutAsync(host, variant.RefId, refilled.Id, 3);

        var result = await AllocateAsync(host, NewKey(), (variant.VariantId, 1));

        Assert.Equal(steady.Id, Assert.Single(result.Allocations).BinId);
    }

    [Fact]
    [Trait("Spec", "ALC-5")]
    public async Task TheSameKeyAgain_IsTheSameAllocation_AndClaimsNothingMore()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 5);
        var key = NewKey();

        await AllocateAsync(host, key, (variant.VariantId, 3));
        var again = await AllocateAsync(host, key, (variant.VariantId, 3));

        Assert.Equal(3, Assert.Single(again.Allocations).Quantity);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Allocations.CountAsync(a => a.OrderKey == key));
    }

    [Fact]
    [Trait("Spec", "ALC-8")]
    public async Task NotEnoughInTheBins_TheShortLineIsReported_AndWhatExistsIsStillAllocated()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 2);

        var result = await AllocateAsync(host, NewKey(), (variant.VariantId, 5));

        Assert.Equal(2, Assert.Single(result.Allocations).Quantity);
        var shortage = Assert.Single(result.Shortages);
        Assert.Equal((variant.VariantId, 5, 2), (shortage.VariantId, shortage.Requested, shortage.Allocated));
    }

    [Fact]
    [Trait("Spec", "ALC-8")]
    public async Task AfterARestock_AskingAgainTopsUpOnlyWhatWasShort()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 2);
        var key = NewKey();
        await AllocateAsync(host, key, (variant.VariantId, 5));

        await PutAsync(host, variant.RefId, bin.Id, 10);
        var topped = await AllocateAsync(host, key, (variant.VariantId, 5));

        Assert.Equal(5, topped.Allocations.Sum(a => a.Quantity));
        Assert.Empty(topped.Shortages);
    }

    [Fact]
    [Trait("Spec", "ALC-6")]
    public async Task ReleasingTheAllocation_FreesItsUnitsForAnotherOrder_AndAskingAgainChangesNothing()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 3);
        var first = NewKey();
        await AllocateAsync(host, first, (variant.VariantId, 3));
        Assert.Single((await AllocateAsync(host, NewKey(), (variant.VariantId, 1))).Shortages);

        await host.InScopeAsync(async sp => { await sp.GetRequiredService<IStockAllocator>().ReleaseAsync(first, CancellationToken.None); return 0; });
        await host.InScopeAsync(async sp => { await sp.GetRequiredService<IStockAllocator>().ReleaseAsync(first, CancellationToken.None); return 0; });

        var other = await AllocateAsync(host, NewKey(), (variant.VariantId, 3));
        Assert.Equal(3, other.Allocations.Sum(a => a.Quantity));
        Assert.True((await AllocateAsync(host, first, (variant.VariantId, 3))).IsClosed);
    }

    [Fact]
    [Trait("Spec", "ALC-9")]
    public async Task TwoOrdersForTheLastUnit_ExactlyOneGetsIt()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => AllocateAsync(host, NewKey(), (variant.VariantId, 1))));

        Assert.Equal(1, results.Count(r => r.Allocations.Count == 1));
        Assert.Equal(5, results.Count(r => r.Shortages.Count == 1));
    }

    [Fact]
    [Trait("Spec", "ALC-10")]
    public async Task AllocatingDoesNotChangeOnHandStock()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 4);

        await AllocateAsync(host, NewKey(), (variant.VariantId, 4));

        Assert.Equal(4, await OnHandAsync(postgres, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "PEX-6")]
    public async Task ABinUnderReview_IsSkipped_UntilItIsCleared()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var flagged = await host.NewBinAsync();
        var other = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, flagged.Id, 5);
        host.Time.Now = host.Time.Now.AddDays(1);
        await PutAsync(host, variant.RefId, other.Id, 5);
        await host.InScopeAsync(async sp => { await sp.GetRequiredService<IStockAllocator>().FlagBinAsync(flagged.Id, "PICK_EXCEPTION", CancellationToken.None); return 0; });

        var skipped = await AllocateAsync(host, NewKey(), (variant.VariantId, 1));
        await host.InScopeAsync(async sp => { await sp.GetRequiredService<IStockAllocator>().ClearBinFlagAsync(flagged.Id, CancellationToken.None); return 0; });
        var cleared = await AllocateAsync(host, NewKey(), (variant.VariantId, 1));

        Assert.Equal(other.Id, Assert.Single(skipped.Allocations).BinId);
        Assert.Equal(flagged.Id, Assert.Single(cleared.Allocations).BinId);
    }

    [Fact]
    [Trait("Spec", "PCK-7")]
    [Trait("Spec", "RSV-9")]
    public async Task ConfirmingThePick_DecrementsEachBinOnce_ConvertsTheReservation_AndKeepsAvailableTheSame()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 5);
        var key = NewKey();
        var reserved = await host.SendAsync(new ReserveStockCommand(key, [new ReservationLineRequest(variant.VariantId, 3)]));
        await host.SendAsync(new ConfirmReservationCommand(key, "RMP-1"));
        var allocation = await AllocateAsync(host, key, (variant.VariantId, 3));

        await host.InScopeAsync(async sp => { await sp.GetRequiredService<IStockAllocator>().ConfirmPickAsync(key, [new PickedUnit(variant.VariantId, bin.Id, 3)], "Picker One", CancellationToken.None); return 0; });

        Assert.True(reserved.IsReserved);
        Assert.Equal(2, await OnHandAsync(postgres, variant.RefId));
        await using var db = postgres.NewContext();
        Assert.Equal(Domain.Reservations.ReservationStatuses.Converted, (await db.Reservations.SingleAsync(r => r.PlacementKey == key)).StatusId);
        Assert.Equal(0, await db.ReservedBalances.Where(b => b.Id == variant.RefId).Select(b => b.Quantity).SingleAsync());
        var movement = await db.Movements.SingleAsync(m => m.VariantRefId == variant.RefId && m.KindId == MovementKinds.Pick);
        Assert.Equal((-3, 5, 2, "Picker One"), (movement.QuantityChange, movement.BalanceBefore, movement.BalanceAfter, movement.ActorName));
        Assert.Equal(AllocationStatuses.Picked, (await db.Allocations.SingleAsync(a => a.OrderKey == key)).StatusId);
        Assert.Equal(3, allocation.Allocations.Sum(a => a.Quantity));
    }

    [Fact]
    [Trait("Spec", "PCK-8")]
    public async Task ConfirmingThePickTwice_DecrementsOnlyOnce()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 5);
        var key = NewKey();
        await AllocateAsync(host, key, (variant.VariantId, 2));
        var picks = new[] { new PickedUnit(variant.VariantId, bin.Id, 2) };

        for (var i = 0; i < 2; i++)
        {
            await host.InScopeAsync(async sp => { await sp.GetRequiredService<IStockAllocator>().ConfirmPickAsync(key, picks, "Picker One", CancellationToken.None); return 0; });
        }

        Assert.Equal(3, await OnHandAsync(postgres, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "PCK-5")]
    public async Task APickLargerThanTheAllocation_IsRefused_AndNothingIsDecremented()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await PutAsync(host, variant.RefId, bin.Id, 5);
        var key = NewKey();
        await AllocateAsync(host, key, (variant.VariantId, 2));

        await Assert.ThrowsAnyAsync<Exception>(() => host.InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IStockAllocator>().ConfirmPickAsync(key, [new PickedUnit(variant.VariantId, bin.Id, 3)], "Picker One", CancellationToken.None);
            return 0;
        }));

        Assert.Equal(5, await OnHandAsync(postgres, variant.RefId));
    }
}
