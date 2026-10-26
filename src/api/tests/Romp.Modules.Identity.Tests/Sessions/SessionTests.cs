using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Tests.Sessions;

public sealed class SessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly SessionLifetimes Defaults = SessionLifetimes.Default;

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Start_StoresOnlyHashes_AndSetsTheDefaultLifetimes()
    {
        var (session, tokens) = Session.Start(7, T0, Defaults);

        Assert.Equal(7, session.UserId);
        Assert.Equal(SessionTokens.Hash(tokens.Access.Reveal()), session.AccessTokenHash);
        Assert.Equal(SessionTokens.Hash(tokens.Refresh.Reveal()), session.RefreshTokenHash);
        Assert.Null(session.PreviousRefreshTokenHash);
        Assert.Equal(T0.AddMinutes(15), session.AccessExpiresAt);
        Assert.Equal(T0.AddMinutes(30), session.RefreshExpiresAt);
        Assert.Equal(T0.AddHours(12), session.AbsoluteExpiresAt);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Rotate_ReplacesBothTokens()
    {
        var (session, first) = Session.Start(7, T0, Defaults);
        var oldRefreshHash = session.RefreshTokenHash;
        var later = T0.AddMinutes(10);

        var second = session.Rotate(first.Refresh.Reveal(), later);

        Assert.Equal(RefreshOutcome.Rotated, second.Outcome);
        Assert.NotNull(second.Tokens);
        Assert.NotEqual(first.Access.Reveal(), second.Tokens.Access.Reveal());
        Assert.NotEqual(first.Refresh.Reveal(), second.Tokens.Refresh.Reveal());
        Assert.Equal(SessionTokens.Hash(second.Tokens.Access.Reveal()), session.AccessTokenHash);
        Assert.Equal(SessionTokens.Hash(second.Tokens.Refresh.Reveal()), session.RefreshTokenHash);
        Assert.Equal(oldRefreshHash, session.PreviousRefreshTokenHash);
        Assert.Equal(later.AddMinutes(15), session.AccessExpiresAt);
        Assert.Equal(later.AddMinutes(30), session.RefreshExpiresAt);
        Assert.Equal(T0.AddHours(12), session.AbsoluteExpiresAt); // the absolute limit never moves
        Assert.False(session.AccessTokenMatches(first.Access.Reveal()));
        Assert.True(session.AccessTokenMatches(second.Tokens.Access.Reveal()));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public void Rotate_NeverExtendsPastTheAbsoluteLimit()
    {
        var lifetimes = new SessionLifetimes(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(12));
        var (session, tokens) = Session.Start(7, T0, lifetimes);

        var result = session.Rotate(tokens.Refresh.Reveal(), T0.AddMinutes(8), lifetimes);

        Assert.Equal(RefreshOutcome.Rotated, result.Outcome);
        Assert.Equal(T0.AddMinutes(12), session.AccessExpiresAt);  // 8 + 5 would be 13
        Assert.Equal(T0.AddMinutes(12), session.RefreshExpiresAt); // 8 + 10 would be 18
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void ReusedRefresh_RevokesTheSession()
    {
        var (session, first) = Session.Start(7, T0, Defaults);
        var second = session.Rotate(first.Refresh.Reveal(), T0.AddMinutes(1));

        var replay = session.Rotate(first.Refresh.Reveal(), T0.AddMinutes(2));

        Assert.Equal(RefreshOutcome.ReuseDetected, replay.Outcome);
        Assert.Null(replay.Tokens);
        Assert.Equal(T0.AddMinutes(2), session.RevokedAt);
        Assert.Equal(SessionRevokeReason.RefreshReuse, session.RevokeReason);
        // Even the legitimate newest value is dead afterwards.
        Assert.Equal(RefreshOutcome.Unusable, session.Rotate(second.Tokens!.Refresh.Reveal(), T0.AddMinutes(3)).Outcome);
        Assert.False(session.IsActive(T0.AddMinutes(3)));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void UnknownRefreshValue_IsRefused_WithoutRevoking()
    {
        var (session, _) = Session.Start(7, T0, Defaults);

        var result = session.Rotate(SessionTokens.Generate().Reveal(), T0.AddMinutes(1));

        Assert.Equal(RefreshOutcome.Unusable, result.Outcome);
        Assert.Null(result.Tokens);
        Assert.Null(session.RevokedAt);
        Assert.Equal(RefreshOutcome.Unusable, session.Rotate("", T0.AddMinutes(1)).Outcome);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public void Expires_IdleAndAbsolute()
    {
        var (session, tokens) = Session.Start(7, T0, Defaults);

        Assert.True(session.AccessIsValid(T0.AddMinutes(14)));
        Assert.False(session.AccessIsValid(T0.AddMinutes(15)));      // access cookie gone
        Assert.True(session.IsActive(T0.AddMinutes(29)));            // still refreshable
        Assert.False(session.IsActive(T0.AddMinutes(30)));           // idle limit

        Assert.Equal(RefreshOutcome.Unusable, session.Rotate(tokens.Refresh.Reveal(), T0.AddMinutes(30)).Outcome);

        // Keep refreshing every 20 minutes: idle never trips, the 12 h absolute limit does.
        var (busy, busyTokens) = Session.Start(8, T0, Defaults);
        var current = busyTokens.Refresh.Reveal();
        var now = T0;
        while (now.AddMinutes(20) < T0.AddHours(12))
        {
            now = now.AddMinutes(20);
            var result = busy.Rotate(current, now);
            Assert.Equal(RefreshOutcome.Rotated, result.Outcome);
            current = result.Tokens!.Refresh.Reveal();
        }

        Assert.True(busy.IsActive(T0.AddHours(12).AddSeconds(-1)));
        Assert.False(busy.IsActive(T0.AddHours(12)));
        Assert.Equal(RefreshOutcome.Unusable, busy.Rotate(current, T0.AddHours(12)).Outcome);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Revoke_IsFinal_AndKeepsTheFirstReason()
    {
        var (session, tokens) = Session.Start(7, T0, Defaults);

        session.Revoke(SessionRevokeReason.SignOut, T0.AddMinutes(1));
        session.Revoke(SessionRevokeReason.RefreshReuse, T0.AddMinutes(2));

        Assert.Equal(T0.AddMinutes(1), session.RevokedAt);
        Assert.Equal(SessionRevokeReason.SignOut, session.RevokeReason);
        Assert.False(session.IsActive(T0.AddMinutes(3)));
        Assert.False(session.AccessIsValid(T0.AddMinutes(3)));
        Assert.Equal(RefreshOutcome.Unusable, session.Rotate(tokens.Refresh.Reveal(), T0.AddMinutes(3)).Outcome);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Lifetimes_AreConfigurable_AndValidated()
    {
        var custom = new SessionLifetimes(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var (session, _) = Session.Start(7, T0, custom);

        Assert.Equal(T0.AddMinutes(5), session.AccessExpiresAt);
        Assert.Equal(T0.AddMinutes(10), session.RefreshExpiresAt);
        Assert.Equal(T0.AddHours(1), session.AbsoluteExpiresAt);

        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionLifetimes(TimeSpan.Zero, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1)));
        // Access cookie cannot outlive the idle window, which cannot outlive the absolute limit.
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionLifetimes(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionLifetimes(TimeSpan.FromMinutes(5), TimeSpan.FromHours(2), TimeSpan.FromHours(1)));
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void ToString_AndTokens_NeverRevealSecrets()
    {
        var (session, tokens) = Session.Start(7, T0, Defaults);

        Assert.DoesNotContain(tokens.Access.Reveal(), tokens.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.Refresh.Reveal(), tokens.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.Refresh.Reveal(), session.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void TenSessionsPerUser_OldestAreEvictedToMakeRoom()
    {
        var sessions = Enumerable.Range(0, 10)
            .Select(i => Session.Start(7, T0.AddMinutes(i), Defaults).Session)
            .ToList();

        Assert.Empty(Session.SelectToEvict(sessions.Take(9).ToList(), T0.AddMinutes(10)));

        var evict = Session.SelectToEvict(sessions, T0.AddMinutes(10));

        Assert.Single(evict);
        Assert.Same(sessions[0], evict.Single());

        // Revoked or expired sessions do not count towards the cap.
        sessions[3].Revoke(SessionRevokeReason.SignOut, T0.AddMinutes(10));
        Assert.Empty(Session.SelectToEvict(sessions, T0.AddMinutes(10)));
    }
}
