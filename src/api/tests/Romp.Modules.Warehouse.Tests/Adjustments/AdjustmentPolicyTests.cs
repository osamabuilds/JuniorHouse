using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;

namespace Romp.Modules.Warehouse.Tests.Adjustments;

public sealed class AdjustmentPolicyTests
{
    private const int Limit = 20;

    [Fact]
    [Trait("Spec", "ADJ-2")]
    public void TheReasonCodes_AreTheFourFromTheBrd()
    {
        Assert.Equal(["DAMAGE", "MISCOUNT", "THEFT", "FOUND_STOCK"],
            new[] { AdjustmentReasons.Damage, AdjustmentReasons.Miscount, AdjustmentReasons.Theft, AdjustmentReasons.FoundStock }.Select(AdjustmentReasons.CodeOf));
        Assert.False(AdjustmentReasons.IsKnown(0));
        Assert.False(AdjustmentReasons.IsKnown(5));
    }

    [Theory]
    [InlineData(AdjustmentReasons.Damage, -3)]
    [InlineData(AdjustmentReasons.Theft, -1)]
    [InlineData(AdjustmentReasons.FoundStock, 4)]
    [InlineData(AdjustmentReasons.Miscount, -2)]
    [InlineData(AdjustmentReasons.Miscount, 2)]
    [Trait("Spec", "ADJ-3")]
    public void ADirectionThatFitsTheReason_IsAllowed(short reason, int change) =>
        AdjustmentPolicy.Check(reason, change, Limit, reconfirmed: false);

    [Theory]
    [InlineData(AdjustmentReasons.Damage, 3)]
    [InlineData(AdjustmentReasons.Theft, 1)]
    [InlineData(AdjustmentReasons.FoundStock, -4)]
    [Trait("Spec", "ADJ-3")]
    public void ADirectionThatContradictsTheReason_IsRefused(short reason, int change)
    {
        var ex = Assert.Throws<DomainException>(() => AdjustmentPolicy.Check(reason, change, Limit, reconfirmed: false));

        Assert.Contains("Use ", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ADJ-1")]
    public void AnUnknownReason_IsRefused() =>
        Assert.Throws<DomainException>(() => AdjustmentPolicy.Check(0, -1, Limit, reconfirmed: false));

    [Theory]
    [InlineData(0)]
    [InlineData(100_001)]
    [InlineData(-100_001)]
    [Trait("Spec", "ADJ-7")]
    public void AZeroOrAbsurdChange_IsRefused(int change) =>
        Assert.Throws<DomainException>(() => AdjustmentPolicy.Check(AdjustmentReasons.Miscount, change, Limit, reconfirmed: true));

    [Theory]
    [InlineData(20)]
    [InlineData(-20)]
    [Trait("Spec", "ADJ-4b")]
    public void AChangeAtTheLimit_NeedsNoReconfirmation(int change) =>
        AdjustmentPolicy.Check(AdjustmentReasons.Miscount, change, Limit, reconfirmed: false);

    [Theory]
    [InlineData(AdjustmentReasons.Miscount, 21)]
    [InlineData(AdjustmentReasons.Damage, -21)]
    [InlineData(AdjustmentReasons.FoundStock, 50)]
    [Trait("Spec", "ADJ-4b")]
    [Trait("Spec", "PUT-21")]
    public void AChangeAboveTheLimit_NeedsAnExplicitReconfirmation_IncludingFoundStock(short reason, int change)
    {
        var ex = Assert.Throws<DomainException>(() => AdjustmentPolicy.Check(reason, change, Limit, reconfirmed: false));

        Assert.StartsWith(AdjustmentMessages.ReconfirmPrefix, ex.Message, StringComparison.Ordinal);
        AdjustmentPolicy.Check(reason, change, Limit, reconfirmed: true);
    }

    [Fact]
    [Trait("Spec", "ADJ-6")]
    public void AnAdjustmentRecord_HasNoMutators()
    {
        var mutable = typeof(StockAdjustment).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .Where(n => n is not ("InsrDte" or "InsrBy" or "UpdtDte" or "UpdtBy"))
            .ToList();

        Assert.Empty(mutable);
    }

    [Fact]
    [Trait("Spec", "ADJ-7")]
    public void ANoteWithControlCharacters_OrTooLong_IsRefused()
    {
        var ok = new StockAdjustment(1, 1, 1, -2, AdjustmentReasons.Damage, "crushed in transit", false, Guid.NewGuid(), null, "Ayesha Khan");
        Assert.Equal("crushed in transit", ok.Note);

        Assert.Throws<DomainException>(() => new StockAdjustment(1, 1, 1, -2, AdjustmentReasons.Damage, new string('x', 201), false, Guid.NewGuid(), null, "Ayesha Khan"));
        Assert.Throws<DomainException>(() => new StockAdjustment(1, 1, 1, -2, AdjustmentReasons.Damage, "line\nbreak", false, Guid.NewGuid(), null, "Ayesha Khan"));
    }
}
