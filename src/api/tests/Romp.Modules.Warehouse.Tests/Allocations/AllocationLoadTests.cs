using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Allocations;

/// <summary>S14 T2 and T3: two hundred orders allocated at the same moment never oversell a bin, never deadlock, and finish quickly (ALC-9, NFR-08).</summary>
[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class AllocationLoadTests(WarehousePostgres postgres)
{
    [Fact]
    [Trait("Spec", "ALC-9")]
    [Trait("Spec", "NFR-08")]
    public async Task TwoHundredOrdersAtOnce_ForExactlyEnoughStock_AllGetTheirUnit_AndNoBinIsOversold()
    {
        using var host = new WarehouseHost(postgres);
        const int variantCount = 20;
        const int perVariant = 10;
        var variants = new List<(long RefId, long VariantId, long BinA, long BinB)>();
        for (var i = 0; i < variantCount; i++)
        {
            var variant = await host.NewVariantAsync();
            var a = await host.NewBinAsync();
            var b = await host.NewBinAsync();
            await host.SendAsync(new TestPostCommand([
                new StockPost(variant.RefId, Grades.Accepted, a.Id, perVariant / 2, MovementKinds.Putaway, 77),
                new StockPost(variant.RefId, Grades.Accepted, b.Id, perVariant / 2, MovementKinds.Putaway, 77)]));
            variants.Add((variant.RefId, variant.VariantId, a.Id, b.Id));
        }

        var keys = Enumerable.Range(0, variantCount * perVariant).Select(n => (Key: $"load-{Guid.NewGuid():N}", Variant: variants[n % variantCount])).ToList();
        var watch = Stopwatch.StartNew();
        var results = new System.Collections.Concurrent.ConcurrentBag<(string Key, long VariantId, AllocationResult Result)>();
        await Parallel.ForEachAsync(keys, new ParallelOptions { MaxDegreeOfParallelism = 25 }, async (item, _) =>
        {
            var result = await host.InScopeAsync(sp => sp.GetRequiredService<IStockAllocator>().AllocateAsync(item.Key, [new AllocationLineRequest(item.Variant.VariantId, 1)], CancellationToken.None));
            results.Add((item.Key, item.Variant.VariantId, result));
        });
        watch.Stop();

        Assert.Equal(variantCount * perVariant, results.Count);
        Assert.All(results, r => Assert.Empty(r.Result.Shortages));
        await using var db = postgres.NewContext();
        foreach (var variant in variants)
        {
            var claims = await db.Allocations.Where(a => a.Lines.Any(l => l.VariantRefId == variant.RefId)).SelectMany(a => a.Lines).Where(l => l.VariantRefId == variant.RefId).ToListAsync();
            Assert.Equal(perVariant, claims.Sum(c => c.Quantity));
            Assert.True(claims.Where(c => c.BinId == variant.BinA).Sum(c => c.Quantity) <= perVariant / 2);
            Assert.True(claims.Where(c => c.BinId == variant.BinB).Sum(c => c.Quantity) <= perVariant / 2);
        }

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"took {watch.Elapsed.TotalSeconds:0.0} s");
    }

    [Fact]
    [Trait("Spec", "ALC-9")]
    public async Task TwoHundredOrdersForHalfTheStock_OnlyTheStockIsAllocated_TheRestAreShort()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([new StockPost(variant.RefId, Grades.Accepted, bin.Id, 100, MovementKinds.Putaway, 77)]));

        var results = new System.Collections.Concurrent.ConcurrentBag<AllocationResult>();
        await Parallel.ForEachAsync(Enumerable.Range(0, 200), new ParallelOptions { MaxDegreeOfParallelism = 25 }, async (_, _) =>
            results.Add(await host.InScopeAsync(sp => sp.GetRequiredService<IStockAllocator>().AllocateAsync($"half-{Guid.NewGuid():N}", [new AllocationLineRequest(variant.VariantId, 1)], CancellationToken.None))));

        Assert.Equal(100, results.Count(r => r.Allocations.Count == 1));
        Assert.Equal(100, results.Count(r => r.Shortages.Count == 1));
    }
}
