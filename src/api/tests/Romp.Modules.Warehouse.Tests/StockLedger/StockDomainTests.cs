using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Tests.StockLedger;

public sealed class StockDomainTests
{
    [Fact]
    [Trait("Spec", "LED-1")]
    public void AStockPost_NeedsAKnownGrade_AKnownKind_APositiveBinAndVariant_AndANonZeroChange()
    {
        Assert.NotNull(new StockPost(1, Grades.Accepted, 2, 5, MovementKinds.Putaway, 9));
        Assert.NotNull(new StockPost(1, Grades.Concession, 2, -5, MovementKinds.Adjustment, 9));

        Assert.Throws<DomainException>(() => new StockPost(1, Grades.Accepted, 2, 0, MovementKinds.Putaway, 9));
        Assert.Throws<DomainException>(() => new StockPost(1, 7, 2, 5, MovementKinds.Putaway, 9));
        Assert.Throws<DomainException>(() => new StockPost(1, Grades.Accepted, 2, 5, 7, 9));
        Assert.Throws<DomainException>(() => new StockPost(0, Grades.Accepted, 2, 5, MovementKinds.Putaway, 9));
        Assert.Throws<DomainException>(() => new StockPost(1, Grades.Accepted, 0, 5, MovementKinds.Putaway, 9));
        Assert.Throws<DomainException>(() => new StockPost(1, Grades.Accepted, 2, 5, MovementKinds.Putaway, 0));
    }

    [Fact]
    [Trait("Spec", "LED-1")]
    public void AMovement_MustBalance_AfterIsBeforePlusTheChange_AndNeverNegative()
    {
        var movement = new StockMovement(1, Grades.Accepted, 2, MovementKinds.Putaway, 9, 4, 10, 14, "Ayesha Khan");

        Assert.Equal(14, movement.BalanceAfter);

        Assert.Throws<DomainException>(() => new StockMovement(1, Grades.Accepted, 2, MovementKinds.Putaway, 9, 4, 10, 13, "Ayesha Khan"));
        Assert.Throws<DomainException>(() => new StockMovement(1, Grades.Accepted, 2, MovementKinds.Adjustment, 9, -4, 3, -1, "Ayesha Khan"));
    }

    [Fact]
    [Trait("Spec", "LED-1")]
    public void ANewGtin_ReplacesTheOld_AndAStaleVoid_IsIgnored()
    {
        var variant = new VariantRef(7, 3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000017");

        variant.ApplyAssigned(3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000024");
        variant.ApplyVoided("0200000000017"); // the old number's void arrives late

        Assert.Equal("0200000000024", variant.Gtin);
        Assert.False(variant.GtinRetired);
    }

    [Fact]
    [Trait("Spec", "PUT-7")]
    public void VoidingTheCurrentGtin_RetiresIt_AndAnAssignmentAfterwardsRevivesTheVariant()
    {
        var variant = new VariantRef(7, 3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000017");

        variant.ApplyVoided("0200000000017");
        Assert.True(variant.GtinRetired);

        variant.ApplyAssigned(3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000024");
        Assert.False(variant.GtinRetired);
    }

    [Fact]
    [Trait("Spec", "LED-4")]
    public void PublishingTheLevel_RaisesOneEvent_WithTheGradeSplit_AndAvailableEqualsOnHand()
    {
        var variant = new VariantRef(7, 3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000017");

        variant.PublishLevel(onHandAccepted: 10, onHandConcession: 2, MovementKinds.CodeOf(MovementKinds.Putaway));

        var raised = Assert.IsType<StockLevelChangedEvent>(Assert.Single(variant.DomainEvents));
        Assert.Equal(12, raised.OnHand);
        Assert.Equal(12, raised.Available);
        Assert.Equal(10, raised.OnHandAccepted);
        Assert.Equal(2, raised.OnHandConcession);
        Assert.Equal("PUTAWAY", raised.ReasonCode);
        Assert.Equal(7, raised.VariantId);
    }
}
