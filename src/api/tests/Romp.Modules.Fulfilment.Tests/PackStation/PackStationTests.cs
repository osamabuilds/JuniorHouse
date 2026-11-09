using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Application.PackStation;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.PackStation;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.PackStation;

/// <summary>The two pure rules (F2) and the pack station flow (F10): a second scan check, packaging, weighing, sealing (PAK-1 to PAK-13).</summary>
public sealed class PackRuleTests
{
    [Theory]
    [Trait("Spec", "PAK-4")]
    [InlineData(1, 330, PackagingKinds.PolyBag)]
    [InlineData(1, 500, PackagingKinds.PolyBag)]
    [InlineData(1, 501, PackagingKinds.Box)]
    [InlineData(2, 330, PackagingKinds.Box)]
    [InlineData(5, 1500, PackagingKinds.Box)]
    public void Packaging_IsAPolyBagOnlyForOneSmallSoftItem(int units, int grams, short expected) =>
        Assert.Equal(expected, PackagingRule.Recommend(units, grams, polyMaxGrams: 500, polyMaxItems: 1));

    [Theory]
    [Trait("Spec", "PAK-6")]
    [InlineData(330, 330, true)]
    [InlineData(380, 330, true)]
    [InlineData(390, 330, true)]
    [InlineData(391, 330, false)]
    [InlineData(268, 330, false)]
    [InlineData(1200, 1000, false)]
    [InlineData(1150, 1000, true)]
    public void AWeight_IsWithinTheLargerOfThePercentageAndTheFixedGrams(int actual, int expected, bool within)
    {
        var check = WeightCheck.Evaluate(actual, expected, tolerancePercent: 15, toleranceGrams: 60);

        Assert.True(check.IsValid);
        Assert.Equal(within, check.IsWithinTolerance);
    }

    [Theory]
    [Trait("Spec", "PAK-8")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(30_001)]
    [InlineData(int.MaxValue)]
    public void AZeroNegativeOrAbsurdWeight_IsRefused(int grams)
    {
        var check = WeightCheck.Evaluate(grams, 330, 15, 60);

        Assert.False(check.IsValid);
        Assert.NotNull(check.Problem);
    }

    [Fact]
    [Trait("Spec", "PAK-8")]
    public void ThirtyKilogramsExactly_IsStillAcceptedAsAWeight() =>
        Assert.True(WeightCheck.Evaluate(30_000, 30_000, 15, 60).IsValid);
}

[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class PackStationTests(FulfilmentPostgres postgres)
{
    private static async Task<(FulfilmentHost Host, long Id, (long OrderId, string OrderNumber, long[] VariantIds, string[] Gtins) Placed)> PickedAsync(
        FulfilmentPostgres postgres, bool cod = true, params (int Quantity, string BinCode, int OnShelf)[] lines)
    {
        var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(cod, lines.Length == 0 ? [(2, "A-01-1", 10)] : lines);
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.PickAndCompleteAsync(host, postgres, id);
        return (host, id, placed);
    }

    private static Task<WeightOutcomeDto?> WeighAsync(FulfilmentHost host, long id, int grams, string staff = FulfilmentHost.Packer) =>
        host.SendAsync(new RecordWeightCommand(id, staff, grams));

    [Fact]
    [Trait("Spec", "PAK-1")]
    public async Task EachUnitScannedAtThePackStation_IsCountedSeparatelyFromThePick()
    {
        var (host, id, placed) = await PickedAsync(postgres);
        using var _ = host;
        await host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer));

        var scan = (await host.SendAsync(new ScanPackCommand(id, FulfilmentHost.Packer, placed.Gtins[0])))!;

        Assert.True(scan.Accepted);
        Assert.Equal((1, 2), (scan.PackedUnits, scan.TotalUnits));
        var line = (await Flow.LoadAsync(postgres, id)).Lines.Single();
        Assert.Equal((2, 1), (line.PickedQuantity, line.PackedQuantity));
    }

    [Fact]
    [Trait("Spec", "PAK-2")]
    public async Task AnItemNotOnTheOrder_OrASurplusUnit_IsRefused_AndNothingIsCounted()
    {
        var (host, id, placed) = await PickedAsync(postgres);
        using var _ = host;
        await Flow.PackScanAllAsync(host, postgres, id);

        var surplus = (await host.SendAsync(new ScanPackCommand(id, FulfilmentHost.Packer, placed.Gtins[0])))!;
        var stranger = (await host.SendAsync(new ScanPackCommand(id, FulfilmentHost.Packer, "2999999999990")))!;

        Assert.False(surplus.Accepted);
        Assert.Equal("SURPLUS_UNIT", surplus.Code);
        Assert.False(stranger.Accepted);
        Assert.Equal("GTIN_NOT_ON_ORDER", stranger.Code);
        Assert.Equal(2, (await Flow.LoadAsync(postgres, id)).Lines.Single().PackedQuantity);
    }

    [Fact]
    [Trait("Spec", "PAK-3")]
    public async Task CompletingWithAMissingUnit_IsRefused_AndTheMissingSkusAreListed()
    {
        var (host, id, placed) = await PickedAsync(postgres, true, (1, "A-01-1", 10), (1, "B-02-2", 10));
        using var _ = host;
        await host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer));
        await host.SendAsync(new ScanPackCommand(id, FulfilmentHost.Packer, placed.Gtins[0]));
        await WeighAsync(host, id, 700);

        var error = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null)));

        Assert.Contains($"SKU-{placed.VariantIds[1]}", error.Message);
        Assert.DoesNotContain($"SKU-{placed.VariantIds[0]}", error.Message);
    }

    [Fact]
    [Trait("Spec", "PAK-4")]
    public async Task ThePackView_ShowsThePackagingByTheRule_AndTheExpectedWeightFromTheSettings()
    {
        var (host, id, _) = await PickedAsync(postgres);
        using var _ = host;

        var view = (await host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer)))!.View!;

        Assert.Equal("BOX", view.Packaging);
        Assert.Equal((2 * host.Options.GarmentGrams) + host.Options.PackagingGrams, view.ExpectedGrams);
    }

    [Fact]
    [Trait("Spec", "PAK-5")]
    [Trait("Spec", "PAK-6")]
    public async Task AWeightThatFits_IsStoredWithTheStaffMember_AndPackingCompletes_WithOneEvent()
    {
        var (host, id, placed) = await PickedAsync(postgres);
        using var _ = host;
        await Flow.PackScanAllAsync(host, postgres, id);
        var expected = (2 * host.Options.GarmentGrams) + host.Options.PackagingGrams;

        var outcome = (await WeighAsync(host, id, expected + 20))!;
        var done = await host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null));

        Assert.True(outcome.WithinTolerance);
        Assert.True(done);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(FulfilmentStatuses.Packed, order.StatusId);
        Assert.Equal((expected + 20, FulfilmentHost.Packer), (order.Pack!.WeightGrams, order.Pack.WeighedBy));
        Assert.False(order.Pack.IsDeviation);
        Assert.Equal(FulfilmentHost.Packer, order.PackedBy);
        Assert.Single(await Flow.EventsAsync(postgres, "OrderPackedEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAK-7")]
    public async Task AWeightOutsideTheTolerance_Warns_NeedsANoteToComplete_AndIsFlaggedForStaff()
    {
        var (host, id, _) = await PickedAsync(postgres);
        using var _ = host;
        await Flow.PackScanAllAsync(host, postgres, id);

        var outcome = (await WeighAsync(host, id, 1200))!;
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null)));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, "   ")));
        await host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, "Gift box included"));

        Assert.False(outcome.WithinTolerance);
        Assert.Contains("over", outcome.Message);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.True(order.Pack!.IsDeviation);
        Assert.Equal("Gift box included", order.Pack.Note);
        Assert.Equal(FulfilmentStatuses.Packed, order.StatusId);
    }

    [Fact]
    [Trait("Spec", "PAK-7")]
    public async Task WeighingAgain_ReplacesTheEarlierWeightAndClearsItsNote()
    {
        var (host, id, _) = await PickedAsync(postgres);
        using var _ = host;
        await Flow.PackScanAllAsync(host, postgres, id);
        var expected = (2 * host.Options.GarmentGrams) + host.Options.PackagingGrams;
        await WeighAsync(host, id, 2000);

        await WeighAsync(host, id, expected);
        await host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null));

        await using var db = postgres.NewContext();
        var packs = await db.PackRecords.Where(p => p.OrderId == id).ToListAsync();
        Assert.Equal(expected, Assert.Single(packs).WeightGrams);
        Assert.False(packs[0].IsDeviation);
    }

    [Theory]
    [Trait("Spec", "PAK-8")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30_001)]
    public async Task AZeroNegativeOrAbsurdWeight_IsRefusedAtTheStation(int grams)
    {
        var (host, id, _) = await PickedAsync(postgres);
        using var _ = host;
        await host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer));

        await Assert.ThrowsAsync<DomainException>(() => WeighAsync(host, id, grams));
        Assert.Null((await Flow.LoadAsync(postgres, id)).Pack);
    }

    [Fact]
    [Trait("Spec", "PAK-10")]
    public async Task AnOrderNotFullyPicked_CannotBeOpenedForPacking()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (2, "A-01-1", 10));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.ClaimAsync(host, id);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer)));
    }

    [Fact]
    [Trait("Spec", "PAK-11")]
    public async Task ACashOnDeliveryOrder_ShowsTheAmountToCollect_AnOnlineOrderShowsNone()
    {
        var (codHost, codId, _) = await PickedAsync(postgres, cod: true);
        var (paidHost, paidId, _) = await PickedAsync(postgres, cod: false);
        using var _a = codHost;
        using var _b = paidHost;

        var cod = (await codHost.SendAsync(new OpenPackCommand(codId, FulfilmentHost.Packer)))!.View!;
        var paid = (await paidHost.SendAsync(new OpenPackCommand(paidId, FulfilmentHost.Packer)))!.View!;

        Assert.True(cod.IsCashOnDelivery);
        Assert.Equal(3200m, cod.CashToCollect);
        Assert.False(paid.IsCashOnDelivery);
        Assert.Equal(0m, paid.CashToCollect);
    }

    [Fact]
    [Trait("Spec", "PAK-13")]
    public async Task CompletingTwice_LeavesOnePackedStateAndOneEvent()
    {
        var (host, id, placed) = await PickedAsync(postgres);
        using var _ = host;
        await Flow.PackScanAllAsync(host, postgres, id);
        await WeighAsync(host, id, (2 * host.Options.GarmentGrams) + host.Options.PackagingGrams);

        var first = await host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null));
        var second = await host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null));

        Assert.True(first);
        Assert.False(second);
        Assert.Single(await Flow.EventsAsync(postgres, "OrderPackedEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAK-12")]
    public async Task APackScan_IsLoggedAtThePackStage_AndAnotherPackerIsToldWhoHoldsTheParcel()
    {
        var (host, id, placed) = await PickedAsync(postgres);
        using var _ = host;
        await host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer));
        await host.SendAsync(new ScanPackCommand(id, FulfilmentHost.Packer, placed.Gtins[0]));

        var other = await host.SendAsync(new OpenPackCommand(id, "Packer Two"));
        var theirScan = (await host.SendAsync(new ScanPackCommand(id, "Packer Two", placed.Gtins[0])))!;

        Assert.Equal(FulfilmentHost.Packer, other!.HeldBy);
        Assert.Null(other.View);
        Assert.False(theirScan.Accepted);
        await using var db = postgres.NewContext();
        Assert.Contains(await db.Scans.Where(s => s.OrderId == placed.OrderId).ToListAsync(), s => s.StageId == ScanStages.Pack && s.Staff == FulfilmentHost.Packer);
    }
}
