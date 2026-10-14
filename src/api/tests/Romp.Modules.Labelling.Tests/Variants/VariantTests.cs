using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Tests.Variants;

public sealed class VariantTests
{
    private static readonly CompanyPrefix Prefix = CompanyPrefix.Parse("0200000");
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static Variant NewVariant()
    {
        var variant = new Variant(7, 3, 5, "ROMP-STY-EDIT-RED-2-3Y");
        typeof(Entity<long>).GetProperty(nameof(Entity<long>.Id))!.SetValue(variant, 11L); // as if saved
        return variant;
    }

    [Fact]
    [Trait("Spec", "VAR-1")]
    [Trait("Spec", "VAR-10")]
    public void AssignGtin_GivesOneActiveGtin_AndRaisesTheAssignedEvent()
    {
        var variant = NewVariant();

        var assigned = variant.AssignGtin(Prefix, 1);

        Assert.Same(assigned, variant.ActiveGtin);
        Assert.Equal(GtinStatus.Active, assigned.StatusId);
        var raised = Assert.IsType<VariantGtinAssignedEvent>(Assert.Single(variant.DomainEvents));
        Assert.Equal(assigned.Gtin, raised.Gtin);
        Assert.Equal("ROMP-STY-EDIT-RED-2-3Y", raised.SkuCode);
        Assert.Equal(11, raised.VariantId);
        Assert.Equal((short)1, raised.SchemaVersion);
    }

    [Fact]
    [Trait("Spec", "VAR-3")]
    public void AssignGtin_WhileOneIsActive_IsRefused()
    {
        var variant = NewVariant();
        variant.AssignGtin(Prefix, 1);

        Assert.Throws<DomainException>(() => variant.AssignGtin(Prefix, 2));
        Assert.Single(variant.Gtins);
    }

    [Fact]
    [Trait("Spec", "VAR-8")]
    [Trait("Spec", "VAR-9")]
    public void Void_RetiresTheGtin_KeepsTheRow_AndAllowsANewNumber()
    {
        var variant = NewVariant();
        var first = variant.AssignGtin(Prefix, 1);
        variant.ClearDomainEvents();

        variant.Void(VoidReasons.Error, "typo in colour", "Asha", Now);

        Assert.Equal(GtinStatus.Retired, first.StatusId);
        Assert.Equal(Now, first.RetiredAt);
        Assert.Equal(VoidReasons.Error, first.RetiredReasonId);
        Assert.Equal("Asha", first.RetiredBy);
        Assert.Null(variant.ActiveGtin);
        var voided = Assert.IsType<GtinVoidedEvent>(Assert.Single(variant.DomainEvents));
        Assert.Equal("Error", voided.ReasonCode);
        Assert.Equal(first.Gtin, voided.Gtin);

        var second = variant.AssignGtin(Prefix, 2);
        Assert.NotEqual(first.Gtin, second.Gtin);
        Assert.Equal(2, variant.Gtins.Count);
    }

    [Fact]
    [Trait("Spec", "VAR-8")]
    public void Void_NeedsAKnownReason_AndAnActiveGtin()
    {
        var variant = NewVariant();
        Assert.Throws<DomainException>(() => variant.Void(VoidReasons.Error, null, "Asha", Now)); // nothing to void

        variant.AssignGtin(Prefix, 1);
        Assert.Throws<DomainException>(() => variant.Void(99, null, "Asha", Now));
    }

    [Theory]
    [InlineData("rOMP-lower")]
    [InlineData("ROMP SPACE")]
    [InlineData("ROMP-<script>")]
    [InlineData("AB")]
    [InlineData("")]
    [Trait("Spec", "VAR-7")]
    public void Constructor_RejectsSkuCodesWithUnsafeCharactersOrBadLength(string sku) =>
        Assert.Throws<DomainException>(() => new Variant(1, 1, 1, sku));
}
