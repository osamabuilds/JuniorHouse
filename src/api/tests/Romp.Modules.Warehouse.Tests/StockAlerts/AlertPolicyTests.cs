using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain.StockAlerts;

namespace Romp.Modules.Warehouse.Tests.StockAlerts;

public sealed class AlertPolicyTests
{
    private static AlertDecision Eval(int available, int threshold = 5, bool everHeld = true, short? open = null) =>
        AlertPolicy.Evaluate(available, threshold, everHeld, open);

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    [InlineData(1)]
    [Trait("Spec", "ALR-1")]
    public void FallingToOrBelowTheThreshold_OpensOneLowAlert(int available) =>
        Assert.Equal(new AlertDecision(AlertAction.Open, AlertLevels.Low), Eval(available));

    [Fact]
    [Trait("Spec", "ALR-1")]
    public void AboveTheThreshold_NothingHappens() =>
        Assert.Equal(AlertAction.None, Eval(6).Action);

    [Fact]
    [Trait("Spec", "ALR-3")]
    public void FurtherMovementsWhileLow_DoNotCreateAnotherAlert() =>
        Assert.Equal(AlertAction.None, Eval(2, open: AlertLevels.Low).Action);

    [Fact]
    [Trait("Spec", "ALR-2")]
    public void ReachingZero_OpensStockOut_OrEscalatesTheOpenLowAlert_NeverASecondOne()
    {
        Assert.Equal(new AlertDecision(AlertAction.Open, AlertLevels.Out), Eval(0));
        Assert.Equal(new AlertDecision(AlertAction.ChangeLevel, AlertLevels.Out), Eval(0, open: AlertLevels.Low));
        Assert.Equal(AlertAction.None, Eval(0, open: AlertLevels.Out).Action);
    }

    [Fact]
    [Trait("Spec", "ALR-3")]
    public void RestockingAboveTheThreshold_ClosesTheAlert_AndAPartialRestockFromZeroBecomesLow()
    {
        Assert.Equal(AlertAction.Close, Eval(20, open: AlertLevels.Low).Action);
        Assert.Equal(AlertAction.Close, Eval(20, open: AlertLevels.Out).Action);
        Assert.Equal(new AlertDecision(AlertAction.ChangeLevel, AlertLevels.Low), Eval(2, open: AlertLevels.Out));
    }

    [Fact]
    [Trait("Spec", "ALR-4")]
    public void AVariantThatNeverHeldStock_RaisesNothing() =>
        Assert.Equal(AlertAction.None, Eval(0, everHeld: false).Action);

    [Fact]
    [Trait("Spec", "ALR-6")]
    public void AThresholdOfZero_AlertsOnlyAtStockOut()
    {
        Assert.Equal(AlertAction.None, Eval(1, threshold: 0).Action);
        Assert.Equal(new AlertDecision(AlertAction.Open, AlertLevels.Out), Eval(0, threshold: 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    [Trait("Spec", "ALR-6")]
    public void AThresholdOutsideTheRange_IsRefused(int quantity) =>
        Assert.Throws<DomainException>(() => new LowStockThreshold(1, quantity));

    [Fact]
    [Trait("Spec", "ALR-7")]
    public void OpeningAndChangingLevel_RaiseTheAlertEvent_ButObservingAndClosingDoNot()
    {
        var at = DateTimeOffset.UtcNow;
        var alert = StockAlert.Open(1, 77, "ROMP-TEST-1", AlertLevels.Low, 4, 5, at);
        var opened = Assert.IsType<StockAlertRaisedEvent>(Assert.Single(alert.DomainEvents));
        Assert.Equal(("LOW", 4, 5, 77), (opened.LevelCode, opened.Available, opened.Threshold, opened.VariantId));

        alert.ClearDomainEvents();
        alert.Observe(3, 5);
        Assert.Empty(alert.DomainEvents);

        alert.ChangeLevel(77, "ROMP-TEST-1", AlertLevels.Out, 0, 5);
        Assert.Equal("OUT", Assert.IsType<StockAlertRaisedEvent>(Assert.Single(alert.DomainEvents)).LevelCode);

        alert.ClearDomainEvents();
        alert.Close(at, 12, 5);
        Assert.Empty(alert.DomainEvents);
        Assert.False(alert.IsOpen);
    }

    [Fact]
    [Trait("Spec", "ALR-5")]
    public void Acknowledging_RecordsWhoAndWhen_StaysOpen_AndAChangeOfLevelClearsIt()
    {
        var at = DateTimeOffset.UtcNow;
        var alert = StockAlert.Open(1, 77, "ROMP-TEST-1", AlertLevels.Low, 4, 5, at);

        alert.Acknowledge("Ayesha Khan", at);
        Assert.True(alert.IsOpen);
        Assert.Equal("Ayesha Khan", alert.AcknowledgedBy);

        alert.ChangeLevel(77, "ROMP-TEST-1", AlertLevels.Out, 0, 5);
        Assert.Null(alert.AcknowledgedBy);

        alert.Close(at, 9, 5);
        Assert.Throws<DomainException>(() => alert.Acknowledge("Ayesha Khan", at));
    }
}
