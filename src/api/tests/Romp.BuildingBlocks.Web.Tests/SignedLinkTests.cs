using Romp.BuildingBlocks.Security;

namespace Romp.BuildingBlocks.Web.Tests;

public sealed class SignedLinkTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static SignedLink Link() => new(Key);

    [Fact]
    [Trait("Spec", "COD-12")]
    public void ATokenRoundTrips_ForTheSamePurpose()
    {
        var token = Link().Create("cod-confirm", "RMP-2026-00123-4");

        Assert.True(Link().TryValidate("cod-confirm", token, Now, out var subject));
        Assert.Equal("RMP-2026-00123-4", subject);
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public void ATokenForOnePurpose_NeverWorksForAnother()
    {
        var token = Link().Create("order-view", "RMP-2026-00123-4");

        Assert.False(Link().TryValidate("cod-confirm", token, Now, out _));
    }

    [Fact]
    [Trait("Spec", "COD-16")]
    public void ATamperedToken_IsRefused()
    {
        var token = Link().Create("cod-confirm", "RMP-2026-00123-4");
        var other = Link().Create("cod-confirm", "RMP-2026-00999-9");
        var forged = other.Split('.')[0] + "." + token.Split('.')[1];

        Assert.False(Link().TryValidate("cod-confirm", forged, Now, out _));
        Assert.False(Link().TryValidate("cod-confirm", token + "x", Now, out _));
        Assert.False(Link().TryValidate("cod-confirm", token[..^3], Now, out _));
    }

    [Fact]
    [Trait("Spec", "COD-16")]
    public void ATokenFromAnotherKey_IsRefused()
    {
        var token = new SignedLink(Enumerable.Range(2, 32).Select(i => (byte)i).ToArray()).Create("cod-confirm", "RMP-1");

        Assert.False(Link().TryValidate("cod-confirm", token, Now, out _));
    }

    [Fact]
    [Trait("Spec", "OLK-5")]
    public void AnExpiredToken_IsRefused_AndOneWithNoExpiryNever()
    {
        var expiring = Link().Create("order-view", "RMP-1", Now.AddMinutes(10));
        var forever = Link().Create("order-view", "RMP-1");

        Assert.True(Link().TryValidate("order-view", expiring, Now.AddMinutes(9), out _));
        Assert.False(Link().TryValidate("order-view", expiring, Now.AddMinutes(11), out _));
        Assert.True(Link().TryValidate("order-view", forever, Now.AddYears(5), out _));
    }

    [Theory]
    [Trait("Spec", "COD-16")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("abc")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    public void Garbage_IsRefusedWithoutThrowing(string? token)
    {
        Assert.False(Link().TryValidate("cod-confirm", token, Now, out var subject));
        Assert.Equal(string.Empty, subject);
    }

    [Fact]
    public void AShortKey_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new SignedLink(new byte[16]));
    }
}
