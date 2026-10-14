using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Tests.Putaway;

public sealed class PutawayItemTests
{
    private static PutawayItem NewItem(int qty = 10, short grade = Grades.Accepted, long? variantRefId = null) =>
        new(subBatchId: 11, qcBatchId: 5, poId: 7, styleId: 500, colourId: 10, sizeId: 1, gradeId: grade, qcQuantity: qty, variantRefId);

    [Fact]
    [Trait("Spec", "PUT-1")]
    public void ANewItem_WaitsForItsGtin_UntilTheVariantIsKnown_WithNothingPutAway()
    {
        var item = NewItem();

        Assert.Equal(PutawayStatuses.AwaitingGtin, item.StatusId);
        Assert.Equal(10, item.Pending);
        Assert.Equal(0, item.PutQuantity);
    }

    [Fact]
    [Trait("Spec", "PUT-1")]
    public void AnItemCreatedWithAKnownVariant_IsPendingRightAway() =>
        Assert.Equal(PutawayStatuses.Pending, NewItem(variantRefId: 42).StatusId);

    [Fact]
    [Trait("Spec", "PUT-2")]
    public void LinkingTheVariant_MovesAnAwaitingItemToPending_AndIsHarmlessTwice()
    {
        var item = NewItem();

        item.LinkVariant(42);
        item.LinkVariant(42);

        Assert.Equal(PutawayStatuses.Pending, item.StatusId);
        Assert.Equal(42, item.VariantRefId);
    }

    [Fact]
    [Trait("Spec", "PUT-5")]
    public void RecordingPartOfTheQuantity_LeavesTheRestPending_AndTheLastUnitsCompleteIt()
    {
        var item = NewItem(10, variantRefId: 42);

        item.Record(4);
        Assert.Equal((6, PutawayStatuses.Pending), (item.Pending, item.StatusId));

        item.Record(6);
        Assert.Equal((0, PutawayStatuses.Complete), (item.Pending, item.StatusId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(11)]
    [Trait("Spec", "PUT-5")]
    [Trait("Spec", "PUT-9")]
    public void AQuantityThatIsNotPositiveOrExceedsWhatIsPending_IsRefused_AndChangesNothing(int qty)
    {
        var item = NewItem(10, variantRefId: 42);

        Assert.Throws<DomainException>(() => item.Record(qty));

        Assert.Equal(10, item.Pending);
    }

    [Fact]
    [Trait("Spec", "PUT-21")]
    public void TheOverQuantityMessage_PointsAtAFoundStockAdjustment()
    {
        var item = NewItem(10, variantRefId: 42);

        var ex = Assert.Throws<DomainException>(() => item.Record(12));

        Assert.Contains("FOUND_STOCK", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PUT-2")]
    public void AnItemWithoutAVariant_CannotBePutAway()
    {
        var item = NewItem();

        var ex = Assert.Throws<DomainException>(() => item.Record(1));

        Assert.Contains("GTIN", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PUT-20")]
    public void TheGrade_ComesFromTheQcOutcome()
    {
        Assert.Equal(Grades.Concession, NewItem(grade: Grades.Concession).GradeId);
        Assert.Equal(Grades.Accepted, PutawayItem.GradeFromOutcome("Accepted"));
        Assert.Equal(Grades.Concession, PutawayItem.GradeFromOutcome("Concession"));
        Assert.Null(PutawayItem.GradeFromOutcome("Rejected"));
        Assert.Null(PutawayItem.GradeFromOutcome("Rework"));
        Assert.Null(PutawayItem.GradeFromOutcome(null));
    }

    [Fact]
    [Trait("Spec", "PUT-1")]
    public void ANonPositiveQcQuantity_IsRefused() =>
        Assert.Throws<DomainException>(() => NewItem(0));

    [Theory]
    [InlineData("2000000000015", true)]
    [InlineData("2000000000017", false)]
    [InlineData("4006381333931", true)]
    [InlineData("4006381333932", false)]
    [InlineData("12345", false)]
    [InlineData("20000000000AB", false)]
    [InlineData("", false)]
    [Trait("Spec", "PUT-6")]
    public void AGtin13_NeedsThirteenDigitsAndAValidGs1CheckDigit(string gtin, bool valid) =>
        Assert.Equal(valid, Gtin13.IsValid(gtin));
}
