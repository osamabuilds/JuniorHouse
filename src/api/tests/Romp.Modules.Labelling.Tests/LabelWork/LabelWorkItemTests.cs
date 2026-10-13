using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;

namespace Romp.Modules.Labelling.Tests.LabelWork;

public sealed class LabelWorkItemTests
{
    [Fact]
    [Trait("Spec", "LW-4")]
    public void New_StartsParkedUntilTheStyleIsResolved()
    {
        var item = new LabelWorkItem(subBatchId: 5, qcBatchId: 2, poId: 9, sizeId: 3, colourId: 4, qty: 12, outcomeId: LabelOutcome.Accepted);

        Assert.Equal(LabelWorkStatus.NeedsAttention, item.StatusId);
        Assert.Equal(ParkReasons.NoStyle, item.ParkReasonId);
        Assert.Null(item.VariantId);
    }

    [Fact]
    [Trait("Spec", "LW-1")]
    public void MarkReady_LinksTheVariant_AndClearsTheParkReason()
    {
        var item = new LabelWorkItem(5, 2, 9, 3, 4, 12, LabelOutcome.Concession);

        item.MarkReady(styleId: 7, variantId: 21);

        Assert.Equal(LabelWorkStatus.Ready, item.StatusId);
        Assert.Null(item.ParkReasonId);
        Assert.Equal(21, item.VariantId);
        Assert.Equal(7, item.StyleId);
    }

    [Fact]
    [Trait("Spec", "LW-4")]
    public void Park_ForARetiredStyle_RemovesTheVariantLink()
    {
        var item = new LabelWorkItem(5, 2, 9, 3, 4, 12, LabelOutcome.Accepted);
        item.MarkReady(7, 21);

        item.Park(ParkReasons.StyleRetired, 7);

        Assert.Equal(LabelWorkStatus.NeedsAttention, item.StatusId);
        Assert.Equal(ParkReasons.StyleRetired, item.ParkReasonId);
        Assert.Null(item.VariantId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [Trait("Spec", "LW-6")]
    public void New_RefusesANonPositiveQuantity(int qty) =>
        Assert.Throws<DomainException>(() => new LabelWorkItem(5, 2, 9, 3, 4, qty, LabelOutcome.Accepted));

    [Theory]
    [InlineData("Accepted", LabelOutcome.Accepted)]
    [InlineData("Concession", LabelOutcome.Concession)]
    [Trait("Spec", "LW-3")]
    public void FromQcCode_MapsTheTwoReleasedOutcomes(string code, short expected) =>
        Assert.Equal(expected, LabelOutcome.FromQcCode(code));

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Rework")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("accepted")]
    [Trait("Spec", "LW-3")]
    public void FromQcCode_RefusesEverythingElse(string? code) =>
        Assert.Null(LabelOutcome.FromQcCode(code));
}
