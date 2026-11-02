using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-11, ACT-12: a session holds only hashes, refresh values rotate and a reused one revokes the session.</summary>
public sealed class CustomerSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "ACT-11")]
    public void Start_StoresOnlyHashes_AndTheTokensPrintAsAMask()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        Assert.Equal(32, session.AccessTokenHash.Length);
        Assert.NotEqual(tokens.Access.Reveal(), Convert.ToBase64String(session.AccessTokenHash));
        Assert.Equal("********", tokens.Access.ToString());
        Assert.True(session.AccessTokenMatches(tokens.Access.Reveal()));
        Assert.False(session.AccessTokenMatches(tokens.Refresh.Reveal()));
        Assert.False(session.AccessTokenMatches(null));
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    public void Start_EveryTimeIssuesNewValues()
    {
        var (_, first) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);
        var (_, second) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        Assert.NotEqual(first.Access.Reveal(), second.Access.Reveal());
        Assert.NotEqual(first.Refresh.Reveal(), second.Refresh.Reveal());
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void StandardLifetimes_AreFifteenMinutesThirtyMinutesAndTwelveHours()
    {
        var (session, _) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        Assert.Equal(Now.AddMinutes(15), session.AccessExpiresAt);
        Assert.Equal(Now.AddMinutes(30), session.RefreshExpiresAt);
        Assert.Equal(Now.AddHours(12), session.AbsoluteExpiresAt);
        Assert.False(session.KeepSignedIn);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void KeepSignedIn_ExtendsTheRefreshLifetimeToThirtyDays()
    {
        var (session, _) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.KeepSignedIn);

        Assert.Equal(Now.AddMinutes(15), session.AccessExpiresAt);
        Assert.Equal(Now.AddDays(30), session.AbsoluteExpiresAt);
        Assert.True(session.KeepSignedIn);
        Assert.True(session.IsActive(Now.AddDays(29)));
        Assert.False(session.IsActive(Now.AddDays(31)));
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void AccessIsValid_OnlyWhileTheShortAccessValueLives()
    {
        var (session, _) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        Assert.True(session.AccessIsValid(Now.AddMinutes(14)));
        Assert.False(session.AccessIsValid(Now.AddMinutes(16)));
        Assert.True(session.IsActive(Now.AddMinutes(16)));
        Assert.False(session.IsActive(Now.AddMinutes(31)));
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void Rotate_WithTheCurrentRefreshValue_IssuesANewPair_AndTheOldAccessValueStopsWorking()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        var result = session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(10));

        Assert.Equal(RefreshOutcome.Rotated, result.Outcome);
        Assert.False(session.AccessTokenMatches(tokens.Access.Reveal()));
        Assert.True(session.AccessTokenMatches(result.Tokens!.Access.Reveal()));
        Assert.Equal(Now.AddMinutes(25), session.AccessExpiresAt);
        Assert.Equal(Now.AddMinutes(40), session.RefreshExpiresAt);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void Rotate_KeepsTheLifetimesTheSessionStartedWith()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.KeepSignedIn);

        session.Rotate(tokens.Refresh.Reveal(), Now.AddDays(2));

        Assert.Equal(Now.AddDays(2).AddMinutes(15), session.AccessExpiresAt);
        Assert.Equal(Now.AddDays(30), session.RefreshExpiresAt);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void Rotate_NeverRunsPastTheAbsoluteLimit()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        var result = session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(20));
        var later = session.Rotate(result.Tokens!.Refresh.Reveal(), Now.AddHours(12).AddMinutes(1));

        Assert.Equal(RefreshOutcome.Unusable, later.Outcome);
        Assert.True(session.RefreshExpiresAt <= session.AbsoluteExpiresAt);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void Rotate_ThePreviousValueAfterTheGrace_RevokesTheSession()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);
        var rotated = session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(1));

        var replay = session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(5));

        Assert.Equal(RefreshOutcome.ReuseDetected, replay.Outcome);
        Assert.Null(replay.Tokens);
        Assert.Equal(CustomerSessionRevokeReason.RefreshReuse, session.RevokeReason);
        Assert.False(session.IsActive(Now.AddMinutes(5)));
        Assert.Equal(RefreshOutcome.Unusable, session.Rotate(rotated.Tokens!.Refresh.Reveal(), Now.AddMinutes(6)).Outcome);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void Rotate_ThePreviousValueWithinTheGrace_IsAConcurrentRetry_NotTheft()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);
        session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(1));

        var retry = session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(1).AddSeconds(3));

        Assert.Equal(RefreshOutcome.ConcurrentRefresh, retry.Outcome);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public void Rotate_AnUnknownValue_ChangesNothing()
    {
        var (session, tokens) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        var result = session.Rotate("not-a-token", Now.AddMinutes(1));

        Assert.Equal(RefreshOutcome.Unusable, result.Outcome);
        Assert.True(session.AccessTokenMatches(tokens.Access.Reveal()));
    }

    [Fact]
    [Trait("Spec", "ACT-13")]
    public void Revoke_IsFinal_ItKeepsTheFirstReason()
    {
        var (session, _) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard);

        session.Revoke(CustomerSessionRevokeReason.SignOut, Now.AddMinutes(1));
        session.Revoke(CustomerSessionRevokeReason.PasswordChanged, Now.AddMinutes(2));

        Assert.Equal(CustomerSessionRevokeReason.SignOut, session.RevokeReason);
        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    public void SelectToEvict_KeepsAtMostTenActiveSessions_DroppingTheOldest()
    {
        var sessions = Enumerable.Range(0, 10)
            .Select(i => CustomerSession.Start(7, Now.AddMinutes(i), CustomerSessionLifetimes.KeepSignedIn).Session)
            .ToList();

        var evict = CustomerSession.SelectToEvict(sessions, Now.AddMinutes(20));

        Assert.Single(evict);
        Assert.Same(sessions[0], evict[0]);
        Assert.Empty(CustomerSession.SelectToEvict(sessions.Skip(1).ToList(), Now.AddMinutes(20)));
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    public void Start_TruncatesLongBrowserText_NeverRejectsIt()
    {
        var (session, _) = CustomerSession.Start(7, Now, CustomerSessionLifetimes.Standard, "203.0.113.9", new string('a', 500));

        Assert.Equal(CustomerSession.UserAgentMaxLength, session.UserAgent!.Length);
        Assert.Equal("203.0.113.9", session.ClientIp);
    }
}
