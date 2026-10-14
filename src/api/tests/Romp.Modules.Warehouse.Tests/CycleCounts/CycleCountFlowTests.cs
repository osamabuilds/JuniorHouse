using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Application.CycleCounts;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.CycleCounts;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.CycleCounts;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class CycleCountFlowTests(WarehousePostgres postgres)
{
    private const string Person = "Ayesha Khan";

    private static StockPost Put(long variant, long bin, int qty, short grade = Grades.Accepted) =>
        new(variant, grade, bin, qty, MovementKinds.Putaway, 77);

    private static async Task<((long RefId, long VariantId, string Gtin, string Sku) Variant, Romp.Modules.Warehouse.Application.Bins.BinRowDto Bin)> StockedAsync(WarehouseHost host, int units)
    {
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, units)]));
        return (variant, bin);
    }

    private static Task<CycleCountDto> Open(WarehouseHost host, params long[] bins) =>
        host.SendAsync(new CreateCycleCountCommand(bins, Person));

    private static Task<CycleCountDto> Count(WarehouseHost host, long countId, string bin, bool recount, params (string Gtin, int Qty)[] lines) =>
        host.SendAsync(new SubmitCountLinesCommand(countId, bin, [.. lines.Select(l => new CountLineInput(l.Gtin, Grades.Accepted, l.Qty))], recount, Person));

    private static CountReasonInput Reason(CountReviewLineDto line, short reason = AdjustmentReasons.Miscount, bool reconfirm = false) =>
        new(line.BinId, line.VariantRefId, line.GradeId, reason, reconfirm);

    private static async Task<int> OnHandAsync(WarehouseHost host, long variantRefId) =>
        await host.InScopeAsync(sp => sp.GetRequiredService<IStockVariantReader>().GetOnHandAsync(variantRefId, default));

    [Fact]
    [Trait("Spec", "CC-1")]
    public async Task ACount_GetsABusinessNumber_IsOpen_AndHasOneEntryPerBin()
    {
        using var host = new WarehouseHost(postgres);
        var (_, binA) = await StockedAsync(host, 5);
        var (_, binB) = await StockedAsync(host, 5);

        var count = await Open(host, binA.Id, binB.Id);

        Assert.Matches(@"^CC-\d{4}-\d{4}$", count.CountNo);
        Assert.Equal("OPEN", count.StatusCode);
        Assert.Equal(2, count.Bins.Count);
        Assert.All(count.Bins, b => Assert.False(b.Submitted));
        Assert.Equal(Person, count.CreatedBy);
    }

    [Fact]
    [Trait("Spec", "CC-3")]
    public async Task ABinInAnOpenCount_IsRefusedForASecondCount_UntilTheFirstIsCancelled()
    {
        using var host = new WarehouseHost(postgres);
        var (_, bin) = await StockedAsync(host, 5);
        var first = await Open(host, bin.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Open(host, bin.Id));
        Assert.Contains("already part of an open count", ex.Message, StringComparison.Ordinal);

        await host.SendAsync(new CancelCycleCountCommand(first.Id, Person));
        Assert.NotNull(await Open(host, bin.Id));
    }

    [Fact]
    [Trait("Spec", "CC-3")]
    public async Task FiveParallelCountsOfTheSameBin_LeaveExactlyOneOpen()
    {
        using var host = new WarehouseHost(postgres);
        var (_, bin) = await StockedAsync(host, 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await Open(host, bin.Id);
                return true;
            }
            catch (Exception ex) when (ex is DomainException or DbUpdateException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(created => created));
    }

    [Fact]
    [Trait("Spec", "CC-1")]
    public async Task AnInactiveOrUnknownBin_CannotBeCounted()
    {
        using var host = new WarehouseHost(postgres);
        var bin = await host.NewBinAsync();
        await host.SendAsync(new Romp.Modules.Warehouse.Application.Bins.SetBinActiveCommand(bin.Id, false, Person));

        await Assert.ThrowsAsync<DomainException>(() => Open(host, bin.Id));
        await Assert.ThrowsAsync<DomainException>(() => Open(host, 987_654_321));
        await Assert.ThrowsAsync<ValidationException>(() => Open(host));
    }

    [Fact]
    [Trait("Spec", "CC-2")]
    public async Task TheBlindCount_NeverCarriesASystemQuantity()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);

        var submitted = await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 8));
        var read = await host.SendAsync(new GetCycleCountQuery(count.Id));

        foreach (var type in new[] { typeof(CycleCountDto), typeof(CountLineDto), typeof(CountBinDto) })
        {
            Assert.DoesNotContain(type.GetProperties(), p => p.Name.Contains("System", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Variance", StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal(8, Assert.Single(read.Lines).CountedQuantity);
        Assert.Equal(8, Assert.Single(submitted.Lines).CountedQuantity);
    }

    [Fact]
    [Trait("Spec", "CC-4")]
    [Trait("Spec", "CC-5")]
    [Trait("Spec", "CC-12")]
    public async Task Confirming_RaisesOneReasonedAdjustmentPerVariance_ClosesTheCount_AndMarksTheBinCounted()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 8));

        var review = await host.SendAsync(new ReviewCountQuery(count.Id));
        var line = Assert.Single(review.Lines);
        Assert.Equal((8, 10, -2, false, false), (line.CountedQuantity, line.SystemQuantity, line.Variance, line.Stale, line.NotCounted));

        var closed = await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(line, AdjustmentReasons.Miscount)], Person));

        Assert.Equal("CLOSED", closed.StatusCode);
        Assert.Equal(8, await OnHandAsync(host, variant.RefId));
        await using var db = postgres.NewContext();
        var adjustment = await db.Adjustments.AsNoTracking().SingleAsync(a => a.VariantRefId == variant.RefId);
        Assert.Equal((-2, AdjustmentReasons.Miscount, Person), (adjustment.QuantityChange, adjustment.ReasonId, adjustment.ActorName));
        Assert.Contains(count.CountNo, adjustment.Note, StringComparison.Ordinal);
        var stored = await db.CycleCounts.AsNoTracking().Include(c => c.Lines).SingleAsync(c => c.Id == count.Id);
        var storedLine = Assert.Single(stored.Lines);
        Assert.Equal((10, adjustment.Id), (storedLine.SystemQuantity, storedLine.AdjustmentId));
        Assert.NotNull((await db.Bins.AsNoTracking().SingleAsync(b => b.Id == bin.Id)).LastCountedAt);
    }

    [Fact]
    [Trait("Spec", "CC-4")]
    public async Task AVarianceWithoutAReason_BlocksTheConfirmation_AndNothingChanges()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 7));

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmCycleCountCommand(count.Id, [], Person)));

        Assert.Contains("Choose a reason", ex.Message, StringComparison.Ordinal);
        Assert.Equal(10, await OnHandAsync(host, variant.RefId));
        Assert.Equal("OPEN", (await host.SendAsync(new GetCycleCountQuery(count.Id))).StatusCode);
    }

    [Fact]
    [Trait("Spec", "CC-8")]
    public async Task AZeroVarianceLine_RaisesNoAdjustment_ButTheBinIsStillMarkedCounted()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 10));

        await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [], Person));

        await using var db = postgres.NewContext();
        Assert.False(await db.Adjustments.AnyAsync(a => a.VariantRefId == variant.RefId));
        Assert.NotNull((await db.Bins.AsNoTracking().SingleAsync(b => b.Id == bin.Id)).LastCountedAt);
    }

    [Fact]
    [Trait("Spec", "CC-6")]
    public async Task AVariantFoundWhereTheSystemExpectsNone_IsAPositiveAdjustment_WithAReason()
    {
        using var host = new WarehouseHost(postgres);
        var (known, bin) = await StockedAsync(host, 4);
        var stray = await host.NewVariantAsync();
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (known.Gtin, 4), (stray.Gtin, 3));

        var review = await host.SendAsync(new ReviewCountQuery(count.Id));
        var strayLine = review.Lines.Single(l => l.VariantRefId == stray.RefId);
        Assert.Equal((3, 0, 3), (strayLine.CountedQuantity, strayLine.SystemQuantity, strayLine.Variance));

        await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(strayLine, AdjustmentReasons.FoundStock)], Person));

        Assert.Equal(3, await OnHandAsync(host, stray.RefId));
    }

    [Fact]
    [Trait("Spec", "CC-4")]
    public async Task AnItemTheSystemHoldsButTheCounterNeverScanned_IsFlaggedNotCounted_AndAdjustedToZero()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 6);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false); // counted the bin and found nothing

        var line = Assert.Single((await host.SendAsync(new ReviewCountQuery(count.Id))).Lines);
        Assert.True(line.NotCounted);
        Assert.Equal(-6, line.Variance);

        await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(line, AdjustmentReasons.Theft)], Person));

        Assert.Equal(0, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "CC-7")]
    public async Task StockThatMovesAfterTheCountOpened_FlagsTheLines_BlocksConfirmation_UntilTheBinIsRecounted()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 5)])); // a putaway lands after the count opened
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 10));

        var review = await host.SendAsync(new ReviewCountQuery(count.Id));
        Assert.All(review.Lines, l => Assert.True(l.Stale));
        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(review.Lines[0])], Person)));
        Assert.Contains("Recount", ex.Message, StringComparison.Ordinal);

        await Count(host, count.Id, bin.BinCode, recount: true, (variant.Gtin, 15));
        var fresh = await host.SendAsync(new ReviewCountQuery(count.Id));
        Assert.All(fresh.Lines, l => Assert.False(l.Stale));
        await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [], Person));
        Assert.Equal(15, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "CC-4")]
    public async Task ConfirmingBeforeEveryBinIsCounted_IsRefused()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, binA) = await StockedAsync(host, 3);
        var (_, binB) = await StockedAsync(host, 3);
        var count = await Open(host, binA.Id, binB.Id);
        await Count(host, count.Id, binA.BinCode, false, (variant.Gtin, 3));

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmCycleCountCommand(count.Id, [], Person)));

        Assert.Contains("Every bin must be counted", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CC-5")]
    public async Task ConfirmingTwice_RaisesTheAdjustmentsOnlyOnce()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 9));
        var line = Assert.Single((await host.SendAsync(new ReviewCountQuery(count.Id))).Lines);

        await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(line)], Person));
        var again = await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(line)], Person));

        Assert.Equal("CLOSED", again.StatusCode);
        Assert.Equal(9, await OnHandAsync(host, variant.RefId));
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Adjustments.CountAsync(a => a.VariantRefId == variant.RefId));
    }

    [Fact]
    [Trait("Spec", "CC-5")]
    [Trait("Spec", "ADJ-4b")]
    public async Task ALargeVariance_NeedsTheReconfirmationOnItsReason()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 100);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 40));
        var line = Assert.Single((await host.SendAsync(new ReviewCountQuery(count.Id))).Lines);

        var refused = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(line, AdjustmentReasons.Theft)], Person)));
        Assert.StartsWith(AdjustmentMessages.ReconfirmPrefix, refused.Message, StringComparison.Ordinal);
        Assert.Equal(100, await OnHandAsync(host, variant.RefId));

        await host.SendAsync(new ConfirmCycleCountCommand(count.Id, [Reason(line, AdjustmentReasons.Theft, reconfirm: true)], Person));
        Assert.Equal(40, await OnHandAsync(host, variant.RefId));
    }

    [Fact]
    [Trait("Spec", "CC-9")]
    public async Task Cancelling_ChangesNoStock_AndFreesTheBins()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var count = await Open(host, bin.Id);
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 1));

        var cancelled = await host.SendAsync(new CancelCycleCountCommand(count.Id, Person));

        Assert.Equal("CANCELLED", cancelled.StatusCode);
        Assert.Equal(10, await OnHandAsync(host, variant.RefId));
        await Assert.ThrowsAsync<DomainException>(() => Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 1)));
    }

    [Fact]
    [Trait("Spec", "CC-10")]
    public async Task AnUnknownBarcode_ARetiredOneCountedAsPresent_ABinOutsideTheCount_AndAnInvalidQuantity_AreRefused()
    {
        using var host = new WarehouseHost(postgres);
        var (variant, bin) = await StockedAsync(host, 10);
        var other = await host.NewBinAsync();
        var count = await Open(host, bin.Id);

        await Assert.ThrowsAsync<DomainException>(() => Count(host, count.Id, bin.BinCode, false, (Gtin13.WithCheckDigit("399999999997"), 1)));
        await Assert.ThrowsAsync<DomainException>(() => Count(host, count.Id, other.BinCode, false, (variant.Gtin, 1)));
        await Assert.ThrowsAsync<ValidationException>(() => Count(host, count.Id, bin.BinCode, false, (variant.Gtin, -1)));
        await Assert.ThrowsAsync<ValidationException>(() => Count(host, count.Id, bin.BinCode, false, ("123", 1)));
        await host.SendAsync(new RecordVariantVoidedCommand(variant.VariantId, variant.Gtin));
        await Assert.ThrowsAsync<DomainException>(() => Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 2)));
        await Count(host, count.Id, bin.BinCode, false, (variant.Gtin, 0)); // counting a retired barcode as absent is fine
    }

    [Fact]
    [Trait("Spec", "CC-11")]
    public async Task TheBinAgeList_CanBeNarrowedToOneZone_SoASpecificBinCanBeFoundAmongManyBins()
    {
        using var host = new WarehouseHost(postgres);
        var zone = WarehouseHost.NewZone();
        var inZone = await host.NewBinAsync(zone);
        await host.NewBinAsync(); // another zone

        var narrowed = await host.SendAsync(new BinAgeQuery(zone.ToLowerInvariant(), PageSize: 100));
        var all = await host.SendAsync(new BinAgeQuery(PageSize: 100));

        Assert.Equal(inZone.Id, Assert.Single(narrowed.Items).BinId);
        Assert.True(all.TotalCount > narrowed.TotalCount);
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new BinAgeQuery("TOOLONG")));
    }

    [Fact]
    [Trait("Spec", "CC-11")]
    public async Task TheCountList_IsPagedNewestFirst_AndTheBinAgeListPutsNeverCountedBinsFirst()
    {
        var counter = new CommandCounter();
        using var host = new WarehouseHost(postgres, counter: counter);
        var (variant, bin) = await StockedAsync(host, 2);
        var first = await Open(host, bin.Id);
        await Count(host, first.Id, bin.BinCode, false, (variant.Gtin, 2));
        await host.SendAsync(new ConfirmCycleCountCommand(first.Id, [], Person));
        var (_, binTwo) = await StockedAsync(host, 2);
        await Open(host, binTwo.Id);

        counter.Reset();
        var page = await host.SendAsync(new SearchCycleCountsQuery(Page: 1, PageSize: 100));
        var open = await host.SendAsync(new SearchCycleCountsQuery(CountStatuses.Open));
        var ages = await host.SendAsync(new BinAgeQuery(PageSize: 100));

        Assert.True(page.Items[0].Id > page.Items[^1].Id);
        Assert.All(open.Items, c => Assert.Equal("OPEN", c.StatusCode));
        var counted = ages.Items.Single(a => a.BinId == bin.Id);
        Assert.NotNull(counted.DaysSinceCounted);
        Assert.True(ages.Items.Select(a => a.LastCountedAt is null ? 0 : 1).SequenceEqual(ages.Items.Select(a => a.LastCountedAt is null ? 0 : 1).OrderBy(x => x)), "never-counted bins come first");
        Assert.True(ages.Items.Single(a => a.BinId == binTwo.Id).InOpenCount);
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SearchCycleCountsQuery(StatusId: 9)));
    }
}
