using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.StockLedger;

/// <summary>
/// Each variant has a stock version that goes up by exactly one with every stock posting that touches it, inside the posting's own
/// transaction (published-catalog task 6; PCT-3, 5, 9, 11). The row lock the increment takes serialises postings of one variant, so
/// version order is commit order - which a ledger-row id cannot promise, because ids are handed out at insert time.
/// </summary>
[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class StockAvailabilityTests(WarehousePostgres postgres)
{
    private static StockPost Put(long variant, long bin, int qty) =>
        new(variant, Grades.Accepted, bin, qty, MovementKinds.Putaway, 77);

    private static Task<T> QueriesAsync<T>(WarehouseHost host, Func<IStockAvailabilityQueries, Task<T>> action) =>
        host.InScopeAsync(sp => action(sp.GetRequiredService<IStockAvailabilityQueries>()));

    private static List<long> LevelEventVersions(WhseDbContext db, string sku) =>
        [.. db.Set<OutboxMessage>().Where(m => m.EventType == "StockLevelChangedEvent").AsEnumerable()
            .Where(m => m.Payload.Contains(sku, StringComparison.Ordinal))
            .Select(m => JsonDocument.Parse(m.Payload).RootElement.GetProperty("Version").GetInt64())
            .Order()];

    [Fact]
    [Trait("Spec", "PCT-3")]
    [Trait("Spec", "PCT-5")]
    public async Task TheFirstPosting_GivesVersionOne_AndEveryLaterPostingAddsOne()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 10)]));
        var first = await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None));
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, -4)]));
        var second = await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None));

        Assert.Equal((variant.VariantId, variant.Sku, 10, 1L), (first!.VariantId, first.SkuCode, first.Available, first.Version));
        Assert.Equal((6, 2L), (second!.Available, second.Version));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task ManyChangesToOneVariantInOnePosting_AddExactlyOne()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var binA = await host.NewBinAsync();
        var binB = await host.NewBinAsync();

        await host.SendAsync(new TestPostCommand([Put(variant.RefId, binA.Id, 6), Put(variant.RefId, binB.Id, 4), Put(variant.RefId, binA.Id, 1)]));

        var availability = await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None));
        Assert.Equal((11, 1L), (availability!.Available, availability.Version));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task TheLevelEvent_CarriesTheSameVersionTheQueryReports()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 10)]));
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 5)]));

        await using var db = postgres.NewContext();
        Assert.Equal([1L, 2L], LevelEventVersions(db, variant.Sku));
        Assert.Equal(2L, (await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None)))!.Version);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task ARefusedPosting_LeavesTheVersionWhereItWas()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 3)]));

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, -9)])));

        Assert.Equal(1L, (await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None)))!.Version);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task TwentyParallelPostingsToOneVariant_GetTwentyDistinctVersions_AndNoQuantityIsLost()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 1)]))));

        await using var db = postgres.NewContext();
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), LevelEventVersions(db, variant.Sku));
        var availability = await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None));
        Assert.Equal((20, 20L), (availability!.Available, availability.Version));
    }

    [Fact]
    [Trait("Spec", "PCT-9")]
    public async Task AVariantThatNeverHadStock_IsNeitherFoundNorListed()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();

        Assert.Null(await QueriesAsync(host, q => q.GetAsync(variant.VariantId, CancellationToken.None)));
        var all = await QueriesAsync(host, q => q.ListAvailabilityAsync(0, 200, CancellationToken.None));
        Assert.DoesNotContain(all, a => a.VariantId == variant.VariantId);
        Assert.Null(await QueriesAsync(host, q => q.GetAsync(long.MaxValue, CancellationToken.None)));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task ListAvailability_PagesByVariantIdWithoutRepeatsOrGaps()
    {
        using var host = new WarehouseHost(postgres);
        var bin = await host.NewBinAsync();
        var variants = new List<(long RefId, long VariantId, string Gtin, string Sku)>();
        for (var i = 0; i < 3; i++)
        {
            var variant = await host.NewVariantAsync();
            variants.Add(variant);
            await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, i + 1)]));
        }

        var after = variants.Min(v => v.VariantId) - 1;
        var first = await QueriesAsync(host, q => q.ListAvailabilityAsync(after, 2, CancellationToken.None));
        var second = await QueriesAsync(host, q => q.ListAvailabilityAsync(first[^1].VariantId, 2, CancellationToken.None));

        var expected = variants.Select(v => v.VariantId).Order().ToList();
        Assert.Equal(expected.Take(2), first.Select(a => a.VariantId));
        Assert.Equal(expected[2], second[0].VariantId);
        Assert.Equal([1, 2, 3], first.Concat(second.Take(1)).Select(a => a.Available).Order());
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void APayloadWrittenBeforeTheVersionExisted_StillReads_AsVersionZero()
    {
        var level = JsonSerializer.Deserialize<StockLevelChangedEvent>(
            "{\"VariantId\":7,\"StyleId\":3,\"ColourId\":2,\"SizeId\":4,\"SkuCode\":\"S\",\"Gtin\":\"G\",\"OnHand\":1,\"Available\":1,\"OnHandAccepted\":1,\"OnHandConcession\":0,\"ReasonCode\":\"PUTAWAY\"}")!;
        var putaway = JsonSerializer.Deserialize<StockPutawayConfirmedEvent>(
            "{\"VariantId\":7,\"StyleId\":3,\"ColourId\":2,\"SizeId\":4,\"SkuCode\":\"S\",\"Gtin\":\"G\",\"GradeCode\":\"ACCEPTED\",\"Quantity\":1,\"AvailableAfter\":1}")!;

        Assert.Equal((0L, 0L), (level.Version, putaway.Version));
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void TheAvailabilityContract_NeverExposesAGtinOrAnyAuditField()
    {
        var names = typeof(StockAvailability).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(["VariantId", "SkuCode", "Available", "Version"], names);
    }
}
