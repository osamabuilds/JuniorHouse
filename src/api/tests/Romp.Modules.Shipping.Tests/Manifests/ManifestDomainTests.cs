using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Manifests;

namespace Romp.Modules.Shipping.Tests.Manifests;

/// <summary>S14 S7 (domain part): the manifest's own rules (MNF-3 to MNF-7).</summary>
public sealed class ManifestDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 17, 0, 0, TimeSpan.Zero);

    private static Manifest WithParcels(params long[] ids)
    {
        var m = Manifest.Open(Couriers.Primary, new DateOnly(2026, 11, 9));
        foreach (var id in ids)
        {
            m.Add(id);
        }

        return m;
    }

    [Fact]
    [Trait("Spec", "MNF-4")]
    public void AddingTheSameParcelTwice_IsOneLine()
    {
        var m = WithParcels(1, 2);

        Assert.False(m.Add(2));
        Assert.Equal(2, m.Lines.Count);
    }

    [Fact]
    [Trait("Spec", "MNF-5")]
    public void ACountEqualToTheLines_IsMatched_AndFinal()
    {
        var m = WithParcels(1, 2, 3);

        m.Count(3, "Ops", T0);

        Assert.Equal(ManifestStatuses.Matched, m.StatusId);
        Assert.True(m.IsFrozen);
        Assert.Equal(("Ops", 3), (m.CountedBy, m.CountedParcels));
        Assert.Throws<DomainException>(() => m.Count(3, "Ops", T0));
        Assert.Throws<DomainException>(() => m.Add(9));
    }

    [Fact]
    [Trait("Spec", "MNF-5")]
    public void ADifferentCount_IsAMismatch()
    {
        var m = WithParcels(1, 2, 3);

        m.Count(2, "Ops", T0);

        Assert.Equal(ManifestStatuses.Mismatch, m.StatusId);
        Assert.False(m.IsFrozen);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void ACountOutOfRange_IsRefused(int count) => Assert.Throws<DomainException>(() => WithParcels(1).Count(count, "Ops", T0));

    [Fact]
    [Trait("Spec", "MNF-6")]
    public void AfterAMismatch_StaffMarkWhatWasHandedOver_AndResolvingReturnsTheRest()
    {
        var m = WithParcels(1, 2, 3);
        m.Count(2, "Ops", T0);

        m.MarkHandedOver(3, false);
        var back = m.Resolve("Courier left one at the depot", "Ops");

        Assert.Equal([3L], back);
        Assert.Equal(ManifestStatuses.Closed, m.StatusId);
        Assert.Equal("Courier left one at the depot", m.Note);
    }

    [Fact]
    [Trait("Spec", "MNF-7")]
    public void AClosedOrMatchedManifest_CannotBeChanged_AndAMarkNeedsAMismatch()
    {
        var matched = WithParcels(1);
        Assert.Throws<DomainException>(() => matched.MarkHandedOver(1, false));
        matched.Count(1, "Ops", T0);
        Assert.Throws<DomainException>(() => matched.Resolve("x", "Ops"));
        var mismatch = WithParcels(1, 2);
        mismatch.Count(1, "Ops", T0);
        Assert.Throws<DomainException>(() => mismatch.MarkHandedOver(99, true));
        Assert.Throws<DomainException>(() => mismatch.Resolve("   ", "Ops"));
    }

    [Fact]
    [Trait("Spec", "MNF-9")]
    public void EveryManifestAction_NeedsASignedInStaffMember()
    {
        Assert.Throws<DomainException>(() => WithParcels(1).Count(1, " ", T0));
        var m = WithParcels(1, 2);
        m.Count(1, "Ops", T0);
        Assert.Throws<DomainException>(() => m.Resolve("note", ""));
    }
}
