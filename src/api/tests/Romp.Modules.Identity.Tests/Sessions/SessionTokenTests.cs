using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Tests.Sessions;

public sealed class SessionTokenTests
{
    [Fact]
    [Trait("Spec", "AC-1")]
    public void Generates_UniqueValues_AndHashesDeterministically()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => SessionTokens.Generate().Reveal()).ToArray();

        Assert.Equal(tokens.Length, tokens.Distinct().Count());
        Assert.All(tokens, t =>
        {
            Assert.Equal(43, t.Length); // 32 bytes, base64url without padding
            Assert.All(t, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        });

        var first = SessionTokens.Hash(tokens[0]);
        Assert.Equal(32, first.Length);
        Assert.Equal(first, SessionTokens.Hash(tokens[0]));
        Assert.NotEqual(first, SessionTokens.Hash(tokens[1]));
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void Matches_AcceptsOnlyTheSameToken_AndNeverThrowsOnJunk()
    {
        var token = SessionTokens.Generate().Reveal();
        var stored = SessionTokens.Hash(token);

        Assert.True(SessionTokens.Matches(token, stored));
        Assert.False(SessionTokens.Matches(SessionTokens.Generate().Reveal(), stored));
        Assert.False(SessionTokens.Matches(string.Empty, stored));
        Assert.False(SessionTokens.Matches(new string('x', 10_000), stored));
        Assert.False(SessionTokens.Matches(token, []));
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void ToString_NeverShowsTheToken()
    {
        var token = SessionTokens.Generate();

        Assert.DoesNotContain(token.Reveal(), token.ToString(), StringComparison.Ordinal);
    }
}
