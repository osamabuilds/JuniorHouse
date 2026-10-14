using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.CycleCounts;

namespace Romp.Modules.Warehouse.Tests.CycleCounts;

public sealed class CycleCountTests
{
    private static CycleCount NewCount(params long[] bins) =>
        new("CC-2026-0001", bins.ToDictionary(b => b, _ => 100L), "Ayesha Khan");

    [Fact]
    [Trait("Spec", "CC-1")]
    public void ANewCount_IsOpen_WithOneEntryPerBin()
    {
        var count = NewCount(1, 2);

        Assert.True(count.IsOpen);
        Assert.Equal(2, count.Bins.Count);
        Assert.All(count.Bins, b => Assert.True(b.IsOpen));
    }

    [Fact]
    [Trait("Spec", "CC-1")]
    public void ACountNeedsBetweenOneAndTwoHundredBins()
    {
        Assert.Throws<DomainException>(() => new CycleCount("CC-1", new Dictionary<long, long>(), "A B"));
        Assert.Throws<DomainException>(() => new CycleCount("CC-1", Enumerable.Range(1, 201).ToDictionary(i => (long)i, _ => 0L), "A B"));
    }

    [Fact]
    [Trait("Spec", "CC-6")]
    public void SettingABinsCounts_ReplacesItsEarlierLines()
    {
        var count = NewCount(1);

        count.SetBinCounts(1, [(10, Grades.Accepted, 5)], recount: false, currentMark: 100);
        count.SetBinCounts(1, [(10, Grades.Accepted, 7), (11, Grades.Concession, 2)], recount: false, currentMark: 100);

        Assert.Equal(2, count.Lines.Count);
        Assert.Equal(7, count.Lines.Single(l => l.VariantRefId == 10).CountedQuantity);
    }

    [Fact]
    [Trait("Spec", "CC-7")]
    public void ARecount_MovesTheBinsMark_ButAPlainSubmitDoesNot()
    {
        var count = NewCount(1);

        count.SetBinCounts(1, [(10, Grades.Accepted, 5)], recount: false, currentMark: 250);
        Assert.Equal(100, count.Bins.Single().OpenMovementMark);

        count.SetBinCounts(1, [(10, Grades.Accepted, 5)], recount: true, currentMark: 250);
        Assert.Equal(250, count.Bins.Single().OpenMovementMark);
    }

    [Fact]
    [Trait("Spec", "CC-10")]
    public void ABinOutsideTheCount_ANegativeQuantity_AndADuplicateLine_AreRefused()
    {
        var count = NewCount(1);

        Assert.Throws<DomainException>(() => count.SetBinCounts(9, [(10, Grades.Accepted, 5)], false, 0));
        Assert.Throws<DomainException>(() => count.SetBinCounts(1, [(10, Grades.Accepted, -1)], false, 0));
        Assert.Throws<DomainException>(() => count.SetBinCounts(1, [(10, Grades.Accepted, 100_001)], false, 0));
        Assert.Throws<DomainException>(() => count.SetBinCounts(1, [(10, Grades.Accepted, 1), (10, Grades.Accepted, 2)], false, 0));
    }

    [Fact]
    [Trait("Spec", "CC-9")]
    public void CancellingAnOpenCount_ClosesItsBins_AndAClosedCountCannotBeChanged()
    {
        var count = NewCount(1);

        count.Cancel();

        Assert.Equal(CountStatuses.Cancelled, count.StatusId);
        Assert.All(count.Bins, b => Assert.False(b.IsOpen));
        Assert.Throws<DomainException>(() => count.Close());
        Assert.Throws<DomainException>(() => count.SetBinCounts(1, [], false, 0));
    }

    [Fact]
    [Trait("Spec", "CC-5")]
    public void ClosingACount_FreesItsBinsForTheNextCount()
    {
        var count = NewCount(1, 2);

        count.Close();

        Assert.Equal(CountStatuses.Closed, count.StatusId);
        Assert.All(count.Bins, b => Assert.False(b.IsOpen));
    }
}
