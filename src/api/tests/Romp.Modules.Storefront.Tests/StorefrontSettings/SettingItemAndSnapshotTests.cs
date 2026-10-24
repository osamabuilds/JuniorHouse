using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using static Romp.Modules.Storefront.Tests.StorefrontSettings.SettingsContentBuilder;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

public sealed class SettingItemAndSnapshotTests
{
    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData("03315613217")]
    [InlineData("+92 331 5613217")]
    [InlineData("+0123456789")]
    [InlineData("hello")]
    public void Validate_BadWhatsappNumber_IsRejectedWithAnExample(string number)
    {
        var problems = SettingsValidator.Validate(Valid(items: [new SettingItem(SettingKey.Whatsapp, number)]));

        Assert.Contains(problems, p => p.Field == "settings.Whatsapp" && p.Message.Contains("+923001234567"));
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_BadEmail_IsRejected() =>
        Assert.Contains(SettingsValidator.Validate(Valid(items: [new SettingItem(SettingKey.Email, "not-an-email")])), p => p.Field == "settings.Email");

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_NegativeCharge_IsRejected()
    {
        var problems = SettingsValidator.Validate(Valid(delivery: [new DeliveryRule("Lahore", 2, 3, -1m, null, "TCS")]));

        Assert.Contains(problems, p => p.Field == "delivery.Lahore" && p.Message.Contains("cannot be negative"));
    }

    [Fact]
    [Trait("Spec", "ST-7")]
    public void Validate_MaxDaysBelowMinDays_IsRejected()
    {
        var problems = SettingsValidator.Validate(Valid(delivery: [new DeliveryRule("Lahore", 5, 2, 250m, null, "TCS")]));

        Assert.Contains(problems, p => p.Message.Contains("longest delivery time"));
    }

    [Theory]
    [Trait("Spec", "ST-7")]
    [InlineData(0)]
    [InlineData(366)]
    public void Validate_ExchangeWindowOutOfRange_IsRejected(int days) =>
        Assert.Contains(SettingsValidator.Validate(Valid(exchange: new ExchangePolicy(days, ["x"]))), p => p.Field == "exchange.window");

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_ControlCharactersInAValue_AreRejected() =>
        Assert.Contains(SettingsValidator.Validate(Valid(items: [new SettingItem(SettingKey.BrandName, "Ro\u0007mp")])), p => p.Message.Contains("control characters"));

    [Fact]
    [Trait("Spec", "ST-14")]
    public void Advance_WithAStaleVersion_Throws()
    {
        var draft = new SettingsDraft();
        draft.Advance(0);

        Assert.Throws<DomainException>(() => draft.Advance(0));
        Assert.Equal(1, draft.Version);
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public void Create_ComputesTheSha256OfTheDocument()
    {
        var snapshot = PublishedSnapshot.Create(1, "{}", "first");

        Assert.Equal("44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a", snapshot.DocumentHash);
        Assert.True(snapshot.IsCurrent);
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public void Create_SameContent_GivesTheSameHash() =>
        Assert.Equal(PublishedSnapshot.Create(1, "{\"a\":1}", null).DocumentHash, PublishedSnapshot.Create(2, "{\"a\":1}", "again").DocumentHash);

    [Fact]
    [Trait("Spec", "ST-16")]
    public void Create_OverTheSizeLimit_Throws() =>
        Assert.Throws<DomainException>(() => PublishedSnapshot.Create(1, new string('x', SettingsLimits.MaxSnapshotBytes + 1), null));
}
