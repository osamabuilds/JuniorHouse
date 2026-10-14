using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Adjustments;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class AdjustmentFlowTests(WarehousePostgres postgres)
{
    private const string Person = "Ayesha Khan";

    private static StockPost Put(long variant, long bin, int qty, short grade = Grades.Accepted) =>
        new(variant, grade, bin, qty, MovementKinds.Putaway, 77);

    private static PostAdjustmentCommand Adjust(
        string gtin, string bin, int change, short reason = AdjustmentReasons.Miscount, bool reconfirm = false, Guid? key = null, short grade = Grades.Accepted, string? note = null) =>
        new(null, gtin, bin, grade, change, reason, note, reconfirm, key ?? Guid.NewGuid(), Person);

    private static async Task<((long RefId, long VariantId, string Gtin, string Sku) Variant, Romp.Modules.Warehouse.Application.Bins.BinRowDto Bin)> StockedAsync(WarehouseHost host, int units)
    {
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, units)]));
        return (variant, bin);
    }

    [Fact]
    [Trait("Spec", "ADJ-0")]
    [Trait("Spec", "TC-STK-01")]
    public async Task MinusThreeWithAReason_ReducesTheBin_AndTheMovementRecordsActorReasonAndBeforeAfter()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);

        var result = await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -3, AdjustmentReasons.Damage, note: "crushed in the aisle"));

        Assert.Equal((-3, "DAMAGE", 10, 7, Person), (result.QuantityChange, result.ReasonCode, result.BalanceBefore, result.BalanceAfter, result.ActorName));
        await using var db = postgres.NewContext();
        var movement = await db.Movements.AsNoTracking().SingleAsync(m => m.KindId == MovementKinds.Adjustment && m.SourceId == result.Id);
        Assert.Equal((-3, 10, 7, Person), (movement.QuantityChange, movement.BalanceBefore, movement.BalanceAfter, movement.ActorName));
        var stored = await db.Adjustments.AsNoTracking().SingleAsync(a => a.Id == result.Id);
        Assert.Equal("crushed in the aisle", stored.Note);
        Assert.NotEqual(default, stored.InsrDte);
    }

    [Fact]
    [Trait("Spec", "ADJ-1")]
    [Trait("Spec", "TC-STK-04")]
    public async Task AnAdjustmentWithoutAReason_IsRefused_AndNothingChanges()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -1, reason: 0)));

        Assert.Equal(10, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-2")]
    public async Task TheReasonLookup_HasTheFourSeededCodes_InOrder()
    {
        using var host = new WarehouseHost(postgres);

        var reasons = await host.SendAsync(new ListAdjustmentReasonsQuery());

        Assert.Equal(["DAMAGE", "MISCOUNT", "THEFT", "FOUND_STOCK"], reasons.Select(r => r.Code));
    }

    [Theory]
    [InlineData(AdjustmentReasons.Damage, 2)]
    [InlineData(AdjustmentReasons.Theft, 2)]
    [InlineData(AdjustmentReasons.FoundStock, -2)]
    [Trait("Spec", "ADJ-3")]
    public async Task AContradictoryDirection_IsRefused(short reason, int change)
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, change, reason)));

        Assert.Equal(10, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-4")]
    [Trait("Spec", "TC-STK-05")]
    public async Task RemovingMoreThanTheBinHolds_IsRefused_AndNothingChanges()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 3);

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -4)));

        Assert.Equal(StockMessages.Insufficient, ex.Message);
        Assert.Equal(3, await OnHandAsync(host, variant.RefId));
        await using var db = postgres.NewContext();
        Assert.False(await db.Adjustments.AnyAsync(a => a.VariantRefId == variant.RefId), "the adjustment record rolled back with the refused posting");
    }

    [Fact]
    [Trait("Spec", "ADJ-4a")]
    [Trait("Spec", "TC-STK-07")]
    public async Task ThirtyMixedAdjustmentsAndPutaways_OnOneBin_LoseNoUpdate()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 100);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(i => i % 2 == 0
            ? host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -1, AdjustmentReasons.Damage)).ContinueWith(_ => 0, TaskScheduler.Default)
            : host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 2)])).ContinueWith(_ => 0, TaskScheduler.Default)));

        // 15 removals of 1 and 15 additions of 2 from 100: 100 - 15 + 30 = 115.
        Assert.Equal(115, await OnHandAsync(host, variant.RefId));
        var mismatches = await host.SendAsync(new StockConsistencyQuery());
        Assert.DoesNotContain(mismatches, m => m.VariantRefId == variant.RefId);
    }

    [Fact]
    [Trait("Spec", "ADJ-4b")]
    [Trait("Spec", "TC-STK-09")]
    public async Task AChangeAboveTheLimit_NeedsTheReconfirmation_ThenApplies()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 100);

        var refused = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -25, AdjustmentReasons.Theft)));
        Assert.StartsWith(AdjustmentMessages.ReconfirmPrefix, refused.Message, StringComparison.Ordinal);
        Assert.Equal(100, await OnHandAsync(host, variant.RefId));

        var applied = await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -25, AdjustmentReasons.Theft, reconfirm: true));
        Assert.True(applied.Reconfirmed);
        Assert.Equal(75, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-4b")]
    public async Task TheLimit_IsConfiguration_NotAConstant()
    {
        using var host = new WarehouseHost(postgres, o => o.LargeAdjustmentUnits = 3);
        var (variant, bin) = await StockedAsync(host, 100);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -4)));
        await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -3));

        Assert.Equal(97, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-5")]
    public async Task TheRecord_TheMovement_AndTheEvent_AreWrittenInOneTransaction()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);

        var result = await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, 5, AdjustmentReasons.FoundStock));

        await using var db = postgres.NewContext();
        Assert.True(await db.Adjustments.AnyAsync(a => a.Id == result.Id));
        Assert.True(await db.Movements.AnyAsync(m => m.SourceId == result.Id && m.KindId == MovementKinds.Adjustment));
        var events = db.Set<OutboxMessage>().Where(m => m.EventType == "StockLevelChangedEvent").AsEnumerable()
            .Count(m => m.Payload.Contains(variant.Sku, StringComparison.Ordinal));
        Assert.Equal(2, events); // the putaway posting and this adjustment
        Assert.Contains("\"ReasonCode\": \"ADJUSTMENT\"", db.Set<OutboxMessage>().OrderBy(m => m.Id).AsEnumerable()
            .Last(m => m.EventType == "StockLevelChangedEvent" && m.Payload.Contains(variant.Sku, StringComparison.Ordinal)).Payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ADJ-6")]
    public void NoAdjustmentRoute_CanEditOrDeleteARecord()
    {
        var methods = typeof(Romp.Modules.Warehouse.Infrastructure.Adjustments.PostAdjustmentRequest).Assembly
            .GetTypes().Where(t => t.Name.Contains("Adjustment", StringComparison.Ordinal) && t.Name.Contains("Update", StringComparison.Ordinal) || t.Name.Contains("DeleteAdjustment", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(methods);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [Trait("Spec", "ADJ-7")]
    public async Task ABadActingPerson_IsRefused(string person)
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);

        await Assert.ThrowsAsync<ValidationException>(() =>
            host.SendAsync(new PostAdjustmentCommand(null, variant.Gtin, bin.BinCode, Grades.Accepted, -1, AdjustmentReasons.Damage, null, false, Guid.NewGuid(), person)));
    }

    [Fact]
    [Trait("Spec", "ADJ-7")]
    public async Task ABadNote_ZeroChange_UnknownBin_UnknownGtin_AndInactiveBin_AreAllRefused()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var off = await host.NewBinAsync();
        await host.SendAsync(new Romp.Modules.Warehouse.Application.Bins.SetBinActiveCommand(off.Id, false, Person));

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -1, note: new string('x', 201))));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -1, note: "bad\nnote")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, 0)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Adjust("123", bin.BinCode, -1)));
        var unknownBin = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, "NO-1-X", -1)));
        var inactive = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, off.BinCode, 1, AdjustmentReasons.FoundStock)));
        var unknownGtin = await Assert.ThrowsAsync<DomainException>(() =>
            host.SendAsync(Adjust(Romp.Modules.Warehouse.Domain.Gtin13.WithCheckDigit("399999999998"), bin.BinCode, -1)));

        Assert.Contains("does not exist", unknownBin.Message, StringComparison.Ordinal);
        Assert.Contains("inactive", inactive.Message, StringComparison.Ordinal);
        Assert.Contains("No variant", unknownGtin.Message, StringComparison.Ordinal);
        Assert.Equal(10, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-8")]
    public async Task TheSameKeyTwice_AppliesOnce_AndReturnsTheOriginal()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var key = Guid.NewGuid();

        var first = await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -2, AdjustmentReasons.Damage, key: key));
        var second = await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -2, AdjustmentReasons.Damage, key: key));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal((10, 8), (second.BalanceBefore, second.BalanceAfter));
        Assert.Equal(8, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-9")]
    public async Task OnARetiredGtin_RemovingIsAllowed_ButAddingIsRefused()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        await host.SendAsync(new RecordVariantVoidedCommand(variant.VariantId, variant.Gtin));

        await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -2, AdjustmentReasons.Damage));
        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Adjust(variant.Gtin, bin.BinCode, 2, AdjustmentReasons.FoundStock)));

        Assert.Contains("retired", ex.Message, StringComparison.Ordinal);
        Assert.Equal(8, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "ADJ-10")]
    public async Task TheHistory_FiltersByReasonAndVariant_IsPagedNewestFirst_AndRunsAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new WarehouseHost(postgres, counter: counter);
        var (variant, bin) = await StockedAsync(host, 100);
        for (var i = 0; i < 3; i++)
        {
            await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -1, AdjustmentReasons.Damage));
        }

        await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, 1, AdjustmentReasons.FoundStock));

        counter.Reset();
        var damage = await host.SendAsync(new SearchAdjustmentsQuery(AdjustmentReasons.Damage, variant.RefId, Page: 1, PageSize: 2));
        var small = counter.Count;
        var all = await host.SendAsync(new SearchAdjustmentsQuery(VariantRefId: variant.RefId, PageSize: 100));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        var dated = await host.SendAsync(new SearchAdjustmentsQuery(VariantRefId: variant.RefId, From: today.AddDays(-1), To: today.AddDays(1)));
        var past = await host.SendAsync(new SearchAdjustmentsQuery(VariantRefId: variant.RefId, From: today.AddDays(-10), To: today.AddDays(-5)));

        Assert.Equal(3, damage.TotalCount);
        Assert.Equal(2, damage.Items.Count);
        Assert.Equal(4, all.TotalCount);
        Assert.True(all.Items[0].Id > all.Items[1].Id);
        Assert.Equal(4, dated.TotalCount);
        Assert.Equal(0, past.TotalCount);

        for (var i = 0; i < 6; i++)
        {
            await host.SendAsync(Adjust(variant.Gtin, bin.BinCode, -1, AdjustmentReasons.Damage));
        }

        counter.Reset();
        await host.SendAsync(new SearchAdjustmentsQuery(AdjustmentReasons.Damage, variant.RefId, Page: 1, PageSize: 2));
        Assert.Equal(small, counter.Count);
    }

    [Fact]
    [Trait("Spec", "ADJ-10")]
    public async Task AnUnknownReasonFilter_OrBackwardsDates_AreRefused()
    {
        using var host = new WarehouseHost(postgres);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SearchAdjustmentsQuery(ReasonId: 9)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SearchAdjustmentsQuery(From: new DateOnly(2026, 10, 2), To: new DateOnly(2026, 10, 1))));
    }

    private static async Task<int> OnHandAsync(WarehouseHost host, long variantRefId) =>
        await host.InScopeAsync(sp => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IStockVariantReader>(sp).GetOnHandAsync(variantRefId, default));
}
