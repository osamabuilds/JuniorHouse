using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-7, ACT-8, ACT-27, ACT-28: reset and verification links are single use, expire and are stored only as hashes.</summary>
public sealed class OneTimeTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "ACT-8")]
    public void PasswordReset_ExpiresAfterSixtyMinutes_AndStoresOnlyTheHash()
    {
        var (token, secret) = PasswordResetToken.Issue(7, Now);

        Assert.Equal(Now.AddMinutes(60), token.ExpiresAt);
        Assert.True(token.IsUsable(Now.AddMinutes(59)));
        Assert.False(token.IsUsable(Now.AddMinutes(61)));
        Assert.Equal(CustomerTokens.Hash(secret.Reveal()), token.TokenHash);
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    public void EmailVerification_ExpiresAfterTwentyFourHours_AndRemembersTheAddressItProves()
    {
        var (token, _) = EmailVerificationToken.Issue(7, "Parent@Example.com", Now);

        Assert.Equal(Now.AddHours(24), token.ExpiresAt);
        Assert.Equal("parent@example.com", token.Email);
    }

    [Fact]
    [Trait("Spec", "ACT-8")]
    public void Consume_WorksOnce_ThenTheLinkIsRefused()
    {
        var (token, secret) = PasswordResetToken.Issue(7, Now);

        Assert.True(token.Consume(secret.Reveal(), Now.AddMinutes(5)));
        Assert.False(token.Consume(secret.Reveal(), Now.AddMinutes(6)));
        Assert.Equal(Now.AddMinutes(5), token.UsedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-8")]
    public void Consume_AnExpiredOrWrongValue_ChangesNothing()
    {
        var (token, secret) = PasswordResetToken.Issue(7, Now);

        Assert.False(token.Consume(secret.Reveal(), Now.AddMinutes(61)));
        Assert.False(token.Consume("nope", Now.AddMinutes(1)));
        Assert.False(token.Consume(null, Now.AddMinutes(1)));
        Assert.Null(token.UsedAt);
        Assert.True(token.Consume(secret.Reveal(), Now.AddMinutes(2)));
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public void Issue_EveryLinkIsDifferent()
    {
        var (_, first) = EmailVerificationToken.Issue(7, "a@example.com", Now);
        var (_, second) = EmailVerificationToken.Issue(7, "a@example.com", Now);

        Assert.NotEqual(first.Reveal(), second.Reveal());
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public void Supersede_MakesAnOlderLinkUnusable()
    {
        var (token, secret) = EmailVerificationToken.Issue(7, "a@example.com", Now);

        token.Supersede(Now.AddMinutes(1));

        Assert.False(token.Consume(secret.Reveal(), Now.AddMinutes(2)));
    }
}
