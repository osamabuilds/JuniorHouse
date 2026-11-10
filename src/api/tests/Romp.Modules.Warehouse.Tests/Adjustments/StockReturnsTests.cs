using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Adjustments;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class StockReturnsTests(WarehousePostgres postgres)
{
    private const string Person = "Ayesha Khan";

    private static RestockCommand Restock(long variant, string bin, int qty, string key, bool seconds = false) =>
        new(new RestockRequest(variant, bin, qty, seconds, key, Person));

    private static Task<int> OnHandAsync(WarehouseHost host, long variantRefId) =>
        host.InScopeAsync(sp => sp.GetRequiredService<IStockVariantReader>().GetOnHandAsync(variantRefId, default));

    [Fact]
    [Trait("Spec", "RQC-4")]
    public async Task Restock_AddsTheUnitsToTheBin_AsAFoundStockAdjustment_WithAGradeAndAnActor()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        var result = await host.SendAsync(Restock(variant.RefId, bin.BinCode, 3, "case:1:line:1"));

        Assert.True(result.Restocked);
        Assert.Equal(3, await OnHandAsync(host, variant.RefId));
        await using var db = postgres.NewContext();
        var adjustment = await db.Adjustments.AsNoTracking().SingleAsync(a => a.VariantRefId == variant.RefId);
        Assert.Equal((3, AdjustmentReasons.FoundStock, Grades.Accepted, Person), (adjustment.QuantityChange, adjustment.ReasonId, adjustment.GradeId, adjustment.ActorName));
    }

    [Fact]
    [Trait("Spec", "RQC-4")]
    public async Task RestockAsSeconds_PutsTheUnitsInTheConcessionGrade()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await host.SendAsync(Restock(variant.RefId, bin.BinCode, 2, "case:2:line:1", seconds: true));

        await using var db = postgres.NewContext();
        var adjustment = await db.Adjustments.AsNoTracking().SingleAsync(a => a.VariantRefId == variant.RefId);
        Assert.Equal(Grades.Concession, adjustment.GradeId);
    }

    [Fact]
    [Trait("Spec", "RQC-9")]
    public async Task TheSameKeyTwice_RestocksOnce()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        var first = await host.SendAsync(Restock(variant.RefId, bin.BinCode, 4, "case:3:line:1"));
        var second = await host.SendAsync(Restock(variant.RefId, bin.BinCode, 4, "case:3:line:1"));

        Assert.True(first.Restocked);
        Assert.True(second.Restocked);
        Assert.Equal(4, await OnHandAsync(host, variant.RefId));
    }

    [Theory]
    [Trait("Spec", "RQC-10")]
    [InlineData("NOSUCHBIN")]
    public async Task AnUnknownBin_IsRefusedWithAPlainMessage_AndNothingChanges(string binCode)
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();

        var result = await host.SendAsync(Restock(variant.RefId, binCode, 1, "case:4:line:1"));

        Assert.False(result.Restocked);
        Assert.Contains("does not exist", result.Refusal);
        Assert.Equal(0, await OnHandAsync(host, variant.RefId));
    }
}
