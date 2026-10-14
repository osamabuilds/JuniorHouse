using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.StockLedger;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class StockQueriesAndEventsTests(WarehousePostgres postgres)
{
    private static StockPost Put(long variant, long bin, int qty, short grade = Grades.Accepted) =>
        new(variant, grade, bin, qty, MovementKinds.Putaway, 77);

    [Fact]
    [Trait("Spec", "LED-5")]
    [Trait("Spec", "LED-6")]
    public async Task StockPerVariant_ShowsOnHandByGrade_AvailableEqualsOnHand_AndEveryBinItSitsIn()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var binA = await host.NewBinAsync();
        var binB = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([
            Put(variant.RefId, binA.Id, 6), Put(variant.RefId, binB.Id, 4), Put(variant.RefId, binB.Id, 3, Grades.Concession)]));

        var page = await host.SendAsync(new SearchStockQuery(variant.Sku));

        var row = Assert.Single(page.Items);
        Assert.Equal((13, 13, 10, 3), (row.OnHand, row.Available, row.OnHandAccepted, row.OnHandConcession));
        Assert.Equal(3, row.Bins.Count);
        Assert.Contains(row.Bins, b => b.BinCode == binB.BinCode && b.GradeCode == "CONCESSION" && b.Quantity == 3);
        Assert.Equal(variant.Gtin, row.Gtin);
    }

    [Fact]
    [Trait("Spec", "LED-5")]
    public async Task AVariantThatHasNoStock_IsNotListed_AndTheGtinFindsTheVariant()
    {
        using var host = new WarehouseHost(postgres);
        var empty = await host.NewVariantAsync();
        var stocked = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(stocked.RefId, bin.Id, 1)]));

        Assert.Empty((await host.SendAsync(new SearchStockQuery(empty.Sku))).Items);
        Assert.Single((await host.SendAsync(new SearchStockQuery(stocked.Gtin))).Items);
    }

    [Fact]
    [Trait("Spec", "LED-7")]
    public async Task ABinsContents_ListEveryVariantAndGradeInIt()
    {
        using var host = new WarehouseHost(postgres);
        var one = await host.NewVariantAsync();
        var two = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(one.RefId, bin.Id, 2), Put(two.RefId, bin.Id, 5, Grades.Concession)]));

        var page = await host.SendAsync(new GetBinContentsQuery(bin.Id));

        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, i => i.Gtin == two.Gtin && i.GradeCode == "CONCESSION" && i.Quantity == 5);
    }

    [Fact]
    [Trait("Spec", "LED-9")]
    public async Task TheHistory_IsNewestFirst_PagedAndFilterable_WithActorAndKind()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        for (var i = 1; i <= 5; i++)
        {
            await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, i)], $"Person {i}"));
        }

        var first = await host.SendAsync(new GetMovementsQuery(VariantRefId: variant.RefId, Page: 1, PageSize: 2));
        var second = await host.SendAsync(new GetMovementsQuery(VariantRefId: variant.RefId, Page: 2, PageSize: 2));

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(["Person 5", "Person 4"], first.Items.Select(m => m.ActorName));
        Assert.Equal(["Person 3", "Person 2"], second.Items.Select(m => m.ActorName));
        Assert.All(first.Items, m => Assert.Equal("PUTAWAY", m.KindCode));
        Assert.Empty((await host.SendAsync(new GetMovementsQuery(VariantRefId: variant.RefId, KindId: MovementKinds.Adjustment))).Items);
    }

    [Fact]
    [Trait("Spec", "LED-8")]
    public async Task TheStockList_RunsAConstantNumberOfQueries_WhateverThePageHolds()
    {
        var counter = new CommandCounter();
        using var host = new WarehouseHost(postgres, counter: counter);
        var bin = await host.NewBinAsync();
        var few = await host.NewVariantAsync();
        await host.SendAsync(new TestPostCommand([Put(few.RefId, bin.Id, 1)]));
        var small = await CountQueriesAsync(host, counter, few.Sku);

        var many = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            var v = await host.NewVariantAsync();
            many.Add(v.Sku);
            await host.SendAsync(new TestPostCommand([Put(v.RefId, bin.Id, 1)]));
        }

        counter.Reset();
        var page = await host.SendAsync(new SearchStockQuery("ROMP-TEST", 1, 50));
        Assert.True(page.Items.Count >= 12);

        Assert.Equal(small, counter.Count);
    }

    private static async Task<int> CountQueriesAsync(WarehouseHost host, CommandCounter counter, string search)
    {
        counter.Reset();
        await host.SendAsync(new SearchStockQuery(search));
        return counter.Count;
    }

    [Fact]
    [Trait("Spec", "LED-8")]
    public async Task ThePageSize_IsClampedToOneHundred()
    {
        using var host = new WarehouseHost(postgres);

        var page = await host.SendAsync(new SearchStockQuery(null, 1, 10_000));

        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public async Task TheRealBinStockQuery_CountsUnitsAcrossVariantsAndGrades_SoABinWithStockCannotBeDeactivated()
    {
        using var host = new WarehouseHost(postgres, realBinStock: true);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 3), Put(variant.RefId, bin.Id, 2, Grades.Concession)]));

        var row = Assert.Single((await host.SendAsync(new Romp.Modules.Warehouse.Application.Bins.SearchBinsQuery(bin.Zone))).Items);
        Assert.Equal(5, row.UnitsOnHand);
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() =>
            host.SendAsync(new Romp.Modules.Warehouse.Application.Bins.SetBinActiveCommand(bin.Id, false, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "PUT-2")]
    public async Task AssigningTheSameVariantTwice_KeepsOneReference_AndAReplacementGtinReplacesTheOld()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();

        await host.SendAsync(new RecordVariantAssignedCommand(variant.VariantId, 1, 10, 1, variant.Sku, variant.Gtin));
        await host.SendAsync(new RecordVariantVoidedCommand(variant.VariantId, variant.Gtin));
        var newGtin = (long.Parse(variant.Gtin, System.Globalization.CultureInfo.InvariantCulture) + 5_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await host.SendAsync(new RecordVariantAssignedCommand(variant.VariantId, 1, 10, 1, variant.Sku, newGtin));

        await using var db = postgres.NewContext();
        var row = await db.VariantRefs.AsNoTracking().SingleAsync(v => v.VariantId == variant.VariantId);
        Assert.Equal(newGtin, row.Gtin);
        Assert.False(row.GtinRetired);
    }

    [Fact]
    [Trait("Spec", "PUT-7")]
    public async Task AVoidedGtin_IsMarkedRetired_AndTheReaderSaysSo()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();

        await host.SendAsync(new RecordVariantVoidedCommand(variant.VariantId, variant.Gtin));

        var info = await host.InScopeAsync(sp => sp.GetRequiredService<IStockVariantReader>().FindByGtinAsync(variant.Gtin, default));
        Assert.NotNull(info);
        Assert.True(info.GtinRetired);
    }

    [Fact]
    [Trait("Spec", "PUT-2")]
    public async Task AVoidForAVariantNeverSeen_IsANoOp() =>
        await new WarehouseHost(postgres).SendAsync(new RecordVariantVoidedCommand(999_999_999, "2000000009999"));

    [Fact]
    [Trait("Spec", "PUT-6")]
    public async Task TheReader_FindsByGtinAndId_AndKnowsWhetherAVariantEverHeldStock()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        var reader = (Func<IServiceProvider, IStockVariantReader>)(sp => sp.GetRequiredService<IStockVariantReader>());
        Assert.False(await host.InScopeAsync(sp => reader(sp).HasEverHeldStockAsync(variant.RefId, default)));
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 2)]));
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, -2)]));

        Assert.True(await host.InScopeAsync(sp => reader(sp).HasEverHeldStockAsync(variant.RefId, default)));
        Assert.Equal(0, await host.InScopeAsync(sp => reader(sp).GetOnHandAsync(variant.RefId, default)));
        Assert.Equal(variant.RefId, (await host.InScopeAsync(sp => reader(sp).FindByIdAsync(variant.RefId, default)))!.Id);
        Assert.Null(await host.InScopeAsync(sp => reader(sp).FindByGtinAsync("2999999999999", default)));
    }

    [Theory]
    [InlineData(0L, 1L, (short)1, (short)1, "ROMP-OK-1", "2000000000017")]
    [InlineData(1L, 0L, (short)1, (short)1, "ROMP-OK-1", "2000000000017")]
    [InlineData(1L, 1L, (short)0, (short)1, "ROMP-OK-1", "2000000000017")]
    [InlineData(1L, 1L, (short)1, (short)0, "ROMP-OK-1", "2000000000017")]
    [InlineData(1L, 1L, (short)1, (short)1, "bad sku!", "2000000000017")]
    [InlineData(1L, 1L, (short)1, (short)1, "ROMP-OK-1", "12345")]
    [InlineData(1L, 1L, (short)1, (short)1, "ROMP-OK-1", "20000000000AB")]
    [Trait("Spec", "PUT-12")]
    public void AMalformedVariantEvent_IsRefused_WithAReason(long variantId, long styleId, short colour, short size, string sku, string gtin)
    {
        var (command, rejection) = VariantEventMapper.Map(new VariantGtinAssignedEvent(variantId, styleId, colour, size, sku, gtin));

        Assert.Null(command);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
    }

    [Fact]
    [Trait("Spec", "PUT-12")]
    public void AWellFormedVariantEvent_IsMapped_AndAMalformedVoidIsRefused()
    {
        Assert.NotNull(VariantEventMapper.Map(new VariantGtinAssignedEvent(1, 2, 3, 4, "ROMP-OK-1", "2000000000017")).Command);
        Assert.NotNull(VariantEventMapper.Map(new GtinVoidedEvent(1, 2, 3, 4, "ROMP-OK-1", "2000000000017", "Error")).Command);
        Assert.Null(VariantEventMapper.Map(new GtinVoidedEvent(0, 2, 3, 4, "ROMP-OK-1", "2000000000017", "Error")).Command);
        Assert.Null(VariantEventMapper.Map(new GtinVoidedEvent(1, 2, 3, 4, "ROMP-OK-1", "nope", "Error")).Command);
    }

    [Fact]
    [Trait("Spec", "LED-2")]
    public async Task TheConsistencyCheck_ReportsNoMismatch_ForTheseBooks()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 7)]));

        var mismatches = await host.SendAsync(new StockConsistencyQuery());

        Assert.DoesNotContain(mismatches, m => m.VariantRefId == variant.RefId);
        _ = PageRequest.DefaultPageSize;
    }
}
