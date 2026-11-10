using Romp.BuildingBlocks.Domain;
using Romp.Modules.Returns.Contracts;
using Romp.Modules.Returns.Domain;
using Romp.Modules.Returns.Domain.ReverseQc;

namespace Romp.Modules.Returns.Tests.ReverseQc;

public sealed class ReturnCaseDomainTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static ReturnCase NewCase(params int[] quantities) =>
        ReturnCase.Open(7, "RMP-7", ReturnKinds.Rto, [.. quantities.Select((q, i) => new ReturnLineSeed(100 + i, $"SKU-{i}", "Frock", "Red", "4-5Y", q))]);

    [Fact]
    [Trait("Spec", "RQC-1")]
    public void Open_StartsAwaitingReceipt_WithTheOrderLines()
    {
        var c = NewCase(2, 1);

        Assert.Equal(ReturnStatuses.AwaitingReceipt, c.StatusId);
        Assert.Equal([2, 1], c.Lines.Select(l => l.OrderedQuantity));
        Assert.Throws<DomainException>(() => ReturnCase.Open(7, "RMP-7", ReturnKinds.Rto, []));
    }

    [Fact]
    [Trait("Spec", "RQC-3")]
    public void Receive_WithoutAnIntactSeal_WaitsForInspection()
    {
        var c = NewCase(2);

        c.Receive(false, " a-01-01 ", "Ayesha Khan", At);

        Assert.Equal((ReturnStatuses.InInspection, false, "A-01-01"), (c.StatusId, c.SealIntact, c.ReceivingBinCode));
    }

    [Fact]
    [Trait("Spec", "RQC-9")]
    public void Receive_Twice_IsRefusedWithAPlainMessage()
    {
        var c = NewCase(2);
        c.Receive(false, "A-01-01", "Ayesha Khan", At);

        var refusal = Assert.Throws<DomainException>(() => c.Receive(false, "A-01-01", "Ayesha Khan", At));

        Assert.Equal("This parcel was already received.", refusal.Message);
    }

    [Theory]
    [Trait("Spec", "RQC-12")]
    [InlineData("", "Ayesha Khan")]
    [InlineData("A-01-01", " ")]
    public void Receive_NeedsABinAndAStaffMember(string bin, string staff)
    {
        var c = NewCase(2);

        Assert.Throws<DomainException>(() => c.Receive(true, bin, staff, At));
        Assert.Equal(ReturnStatuses.AwaitingReceipt, c.StatusId);
    }

    [Fact]
    [Trait("Spec", "RQC-7")]
    public void Complete_WithUndecidedLines_SaysHowManyAreWaiting()
    {
        var c = NewCase(2, 1);
        c.Receive(false, "A-01-01", "Ayesha Khan", At);

        var refusal = Assert.Throws<DomainException>(() => c.Complete("Ayesha Khan", At));

        Assert.Equal("2 lines are still waiting for a decision.", refusal.Message);
        Assert.Equal(ReturnStatuses.InInspection, c.StatusId);
    }

    [Fact]
    [Trait("Spec", "RQC-8")]
    public void BeforeCompletion_NoResultIsAnnounced()
    {
        var c = NewCase(2);

        c.Receive(false, "A-01-01", "Ayesha Khan", At);

        Assert.DoesNotContain(c.DomainEvents, e => e is ReturnQcCompletedEvent);
        Assert.Throws<DomainException>(() => NewCase(1).Complete("Ayesha Khan", At));
    }
}
