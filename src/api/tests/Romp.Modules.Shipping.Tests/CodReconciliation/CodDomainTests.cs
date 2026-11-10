using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;
using Romp.Modules.Shipping.Domain.CodReconciliation;

namespace Romp.Modules.Shipping.Tests.CodReconciliation;

/// <summary>S16 C1 to C3: the pure rules of COD reconciliation, with no database (COD-1 to COD-8, COD-11, COD-12, COD-16).</summary>
public sealed class CodDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private static CodParcelFacts Parcel(bool delivered = true, decimal cash = 4700m, short status = CodStatuses.Awaiting) => new(delivered, cash, status);

    [Theory]
    [Trait("Spec", "COD-2")]
    [Trait("Spec", "COD-3")]
    [InlineData(4700, RemittanceOutcomes.Matched)]
    [InlineData(4699.99, RemittanceOutcomes.ShortPaid)]
    [InlineData(4700.01, RemittanceOutcomes.Over)]
    public void Judge_AmountsAgainstTheCash_AreExactToThePaisa(double amount, short expected) =>
        Assert.Equal(expected, CodJudge.Judge(Parcel(), (decimal)amount));

    [Fact]
    [Trait("Spec", "COD-5")]
    public void Judge_NoParcel_IsUnknown() => Assert.Equal(RemittanceOutcomes.Unknown, CodJudge.Judge(null, 100m));

    [Fact]
    [Trait("Spec", "COD-6")]
    public void Judge_ANotYetDeliveredParcel_IsNotDelivered() => Assert.Equal(RemittanceOutcomes.NotDelivered, CodJudge.Judge(Parcel(delivered: false), 4700m));

    [Fact]
    [Trait("Spec", "COD-7")]
    public void Judge_AParcelWithNoCash_IsNotCod() => Assert.Equal(RemittanceOutcomes.NotCod, CodJudge.Judge(Parcel(cash: 0m, status: CodStatuses.NotCod), 100m));

    [Theory]
    [Trait("Spec", "COD-4")]
    [InlineData(CodStatuses.Settled)]
    [InlineData(CodStatuses.ShortPaid)]
    [InlineData(CodStatuses.Over)]
    [InlineData(CodStatuses.Resolved)]
    public void Judge_AParcelAlreadyPaid_IsADuplicate(short status) => Assert.Equal(RemittanceOutcomes.Duplicate, CodJudge.Judge(Parcel(status: status), 4700m));

    private static Shipment Delivered(decimal cash = 4700m)
    {
        var s = Shipment.Create(5, "RMP-5", cash, 600, T0);
        s.MarkBooked(Couriers.Primary, "PRM-000001", BookingSources.Courier, T0);
        s.Apply(ShipmentStatuses.Delivered, "e1", T0.AddDays(1), EventSources.Push);
        return s;
    }

    [Fact]
    [Trait("Spec", "COD-1")]
    public void ANewShipment_IsAwaitingWhenThereIsCash_AndNotCodWhenThereIsNone()
    {
        Assert.Equal(CodStatuses.Awaiting, Shipment.Create(1, "RMP-1", 100m, 500, T0).CodStatusId);
        Assert.Equal(CodStatuses.NotCod, Shipment.Create(2, "RMP-2", 0m, 500, T0).CodStatusId);
    }

    [Theory]
    [Trait("Spec", "COD-2")]
    [Trait("Spec", "COD-3")]
    [InlineData(RemittanceOutcomes.Matched, CodStatuses.Settled)]
    [InlineData(RemittanceOutcomes.ShortPaid, CodStatuses.ShortPaid)]
    [InlineData(RemittanceOutcomes.Over, CodStatuses.Over)]
    [InlineData(RemittanceOutcomes.Duplicate, CodStatuses.Awaiting)]
    [InlineData(RemittanceOutcomes.Unknown, CodStatuses.Awaiting)]
    public void ApplyRemittance_MovesAnAwaitingParcelOnlyForMatchedShortAndOver(short outcome, short expected)
    {
        var s = Delivered();

        s.ApplyRemittance(outcome);

        Assert.Equal(expected, s.CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-4")]
    public void ApplyRemittance_ASettledParcelStaysSettled()
    {
        var s = Delivered();
        s.ApplyRemittance(RemittanceOutcomes.Matched);

        s.ApplyRemittance(RemittanceOutcomes.Over);

        Assert.Equal(CodStatuses.Settled, s.CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-11")]
    public void FlagCodOverdue_OnlyAfterTheCycle_OnlyOnce_AndClearsOnSettlement()
    {
        var s = Delivered();

        Assert.False(s.FlagCodOverdue(T0.AddDays(7), 7));
        Assert.True(s.FlagCodOverdue(T0.AddDays(8), 7));
        Assert.False(s.FlagCodOverdue(T0.AddDays(9), 7));
        Assert.NotNull(s.CodOverdueAt);

        s.ApplyRemittance(RemittanceOutcomes.Matched);
        Assert.Null(s.CodOverdueAt);
    }

    [Fact]
    [Trait("Spec", "COD-11")]
    public void FlagCodOverdue_NeverFlagsAParcelNotDeliveredOrNotCod()
    {
        var undelivered = Shipment.Create(5, "RMP-5", 4700m, 600, T0);
        Assert.False(undelivered.FlagCodOverdue(T0.AddDays(30), 7));
        Assert.False(Delivered(0m).FlagCodOverdue(T0.AddDays(30), 7));
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public void ResolveCod_ClosesAShortOrOverParcel_ButLeavesASettledOne()
    {
        var shortPaid = Delivered();
        shortPaid.ApplyRemittance(RemittanceOutcomes.ShortPaid);
        shortPaid.ResolveCod();
        var settled = Delivered();
        settled.ApplyRemittance(RemittanceOutcomes.Matched);
        settled.ResolveCod();

        Assert.Equal(CodStatuses.Resolved, shortPaid.CodStatusId);
        Assert.Equal(CodStatuses.Settled, settled.CodStatusId);
    }

    private static readonly CodStatementLine[] TwoLines = [new("prm-1", 100m), new(" PRM-2 ", 200.50m)];

    [Fact]
    [Trait("Spec", "COD-8")]
    public void Validate_CleansTheReferenceAndWaybills_WhenTheTotalEqualsTheLines()
    {
        var (reference, lines) = CodRemittance.Validate(" st-001 ", 300.50m, TwoLines);

        Assert.Equal("ST-001", reference);
        Assert.Equal(["PRM-1", "PRM-2"], lines.Select(l => l.Waybill));
    }

    [Fact]
    [Trait("Spec", "COD-8")]
    public void Validate_ATotalThatIsNotTheSum_IsRefused() =>
        Assert.Throws<DomainException>(() => CodRemittance.Validate("ST-1", 300m, TwoLines));

    [Theory]
    [Trait("Spec", "COD-16")]
    [InlineData("", 100)]
    [InlineData("bad ref!", 100)]
    [InlineData("ST-1", 0)]
    [InlineData("ST-1", -5)]
    [InlineData("ST-1", 10000000)]
    [InlineData("ST-1", 10.123)]
    public void Validate_BadReferenceOrAmount_IsRefused(string reference, double amount) =>
        Assert.Throws<DomainException>(() => CodRemittance.Validate(reference, (decimal)amount, [new("PRM-1", (decimal)amount)]));

    [Fact]
    [Trait("Spec", "COD-16")]
    public void Validate_NoLines_TooManyLines_AndABadWaybill_AreRefused()
    {
        Assert.Throws<DomainException>(() => CodRemittance.Validate("ST-1", 0m, []));
        var many = Enumerable.Range(0, CodRemittance.MaxLines + 1).Select(i => new CodStatementLine($"W{i}", 1m)).ToList();
        Assert.Throws<DomainException>(() => CodRemittance.Validate("ST-1", many.Count, many));
        Assert.Throws<DomainException>(() => CodRemittance.Validate("ST-1", 1m, [new("bad waybill!", 1m)]));
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public void ALine_IsResolvedOnce_WithANoteAndAPerson_AndAMatchedLineNeedsNone()
    {
        var remittance = CodRemittance.Record(Couriers.Primary, "ST-1", new DateOnly(2026, 11, 10), 100m);
        var flagged = remittance.AddLine("PRM-1", 100m, null, RemittanceOutcomes.Unknown);
        var matched = remittance.AddLine("PRM-2", 100m, 5, RemittanceOutcomes.Matched);

        Assert.Throws<DomainException>(() => flagged.Resolve("", "Finance", T0));
        Assert.Throws<DomainException>(() => flagged.Resolve("ok", "", T0));
        Assert.Throws<DomainException>(() => matched.Resolve("ok", "Finance", T0));
        flagged.Resolve("Courier confirmed a typo", "Finance", T0);
        Assert.Equal(("Courier confirmed a typo", "Finance", false), (flagged.ResolutionNote, flagged.ResolvedBy, flagged.IsOpen));
        Assert.Throws<DomainException>(() => flagged.Resolve("again", "Finance", T0));
    }
}
