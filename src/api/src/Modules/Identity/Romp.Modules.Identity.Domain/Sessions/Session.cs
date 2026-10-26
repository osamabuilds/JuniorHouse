using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Domain.Sessions;

/// <summary>How long a session's parts live (sessions AC-2, AC-5). Access cookie, idle window and absolute limit, in that order.</summary>
public sealed record SessionLifetimes
{
    public static readonly SessionLifetimes Default = new(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

    public SessionLifetimes(TimeSpan access, TimeSpan idle, TimeSpan absolute)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(access, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(idle, access);
        ArgumentOutOfRangeException.ThrowIfLessThan(absolute, idle);
        Access = access;
        Idle = idle;
        Absolute = absolute;
    }

    public TimeSpan Access { get; }

    public TimeSpan Idle { get; }

    public TimeSpan Absolute { get; }
}

/// <summary>The cookie values handed out once; only their hashes are stored. Printing this shows masks.</summary>
public sealed record IssuedTokens(SecretString Access, SecretString Refresh);

public enum RefreshOutcome
{
    /// <summary>The presented refresh value was current; a new pair was issued.</summary>
    Rotated = 1,

    /// <summary>An already rotated value was presented again; the session is now revoked.</summary>
    ReuseDetected = 2,

    /// <summary>Unknown value, or the session is revoked or expired. Nothing changed.</summary>
    Unusable = 3,
}

public sealed record RefreshResult(RefreshOutcome Outcome, IssuedTokens? Tokens);

/// <summary>Why a session ended; stored through the SESS_RVKD_RSN_LKP lookup (ids are stable).</summary>
public enum SessionRevokeReason : short
{
    SignOut = 1,
    SignOutEverywhere = 2,
    RefreshReuse = 3,
    NewSignIn = 4,
    PasswordChanged = 5,
    AccountDeactivated = 6,
    AdminRevoked = 7,
    SessionLimit = 8,
}

/// <summary>
/// IDNT.SESS_MAIN: a signed-in staff session (sessions). Holds the hashes of the access and refresh values, never the values.
/// A refresh value is single use: presenting the previous one again means it leaked, so the whole session is revoked (AC-3).
/// </summary>
public sealed class Session : AggregateRoot<long>, IAuditable
{
    public const int MaxPerUser = 10;

    private Session()
    {
        AccessTokenHash = [];
        RefreshTokenHash = [];
        InsrBy = string.Empty;
    }

    public long UserId { get; private set; }

    public byte[] AccessTokenHash { get; private set; }

    public byte[] RefreshTokenHash { get; private set; }

    public byte[]? PreviousRefreshTokenHash { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset AccessExpiresAt { get; private set; }

    public DateTimeOffset RefreshExpiresAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public SessionRevokeReason? RevokeReason { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static (Session Session, IssuedTokens Tokens) Start(long userId, DateTimeOffset now, SessionLifetimes lifetimes)
    {
        ArgumentNullException.ThrowIfNull(lifetimes);

        var tokens = new IssuedTokens(SessionTokens.Generate(), SessionTokens.Generate());
        var session = new Session
        {
            UserId = userId,
            StartedAt = now,
            AbsoluteExpiresAt = now + lifetimes.Absolute,
        };
        session.Apply(tokens, now, lifetimes);
        return (session, tokens);
    }

    /// <summary>Not revoked, inside the absolute limit and inside the idle window: the session can still be refreshed.</summary>
    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && now < AbsoluteExpiresAt && now < RefreshExpiresAt;

    /// <summary>Active and the short-lived access value has not expired.</summary>
    public bool AccessIsValid(DateTimeOffset now) => IsActive(now) && now < AccessExpiresAt;

    public bool AccessTokenMatches(string? presented) => SessionTokens.Matches(presented, AccessTokenHash);

    /// <summary>
    /// Exchanges the current refresh value for a new pair. <paramref name="lifetimes"/> is the configured set; the default applies
    /// only when the caller has none. Idle and access expiry never run past the absolute limit.
    /// </summary>
    public RefreshResult Rotate(string? presentedRefresh, DateTimeOffset now, SessionLifetimes? lifetimes = null)
    {
        if (!IsActive(now))
        {
            return new RefreshResult(RefreshOutcome.Unusable, null);
        }

        if (SessionTokens.Matches(presentedRefresh, RefreshTokenHash))
        {
            var tokens = new IssuedTokens(SessionTokens.Generate(), SessionTokens.Generate());
            PreviousRefreshTokenHash = RefreshTokenHash;
            Apply(tokens, now, lifetimes ?? SessionLifetimes.Default);
            return new RefreshResult(RefreshOutcome.Rotated, tokens);
        }

        if (PreviousRefreshTokenHash is not null && SessionTokens.Matches(presentedRefresh, PreviousRefreshTokenHash))
        {
            Revoke(SessionRevokeReason.RefreshReuse, now);
            return new RefreshResult(RefreshOutcome.ReuseDetected, null);
        }

        return new RefreshResult(RefreshOutcome.Unusable, null);
    }

    /// <summary>Ends the session. Final: a second call keeps the first date and reason.</summary>
    public void Revoke(SessionRevokeReason reason, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokeReason = reason;
    }

    /// <summary>
    /// The sessions to revoke so that starting one more keeps the user within <see cref="MaxPerUser"/>: the oldest active ones.
    /// Revoked and expired sessions do not count.
    /// </summary>
    public static IReadOnlyList<Session> SelectToEvict(IReadOnlyCollection<Session> userSessions, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userSessions);

        var active = userSessions.Where(s => s.IsActive(now)).OrderBy(s => s.StartedAt).ToList();
        var excess = active.Count - (MaxPerUser - 1);
        return excess <= 0 ? [] : active.Take(excess).ToList();
    }

    private void Apply(IssuedTokens tokens, DateTimeOffset now, SessionLifetimes lifetimes)
    {
        AccessTokenHash = SessionTokens.Hash(tokens.Access.Reveal());
        RefreshTokenHash = SessionTokens.Hash(tokens.Refresh.Reveal());
        AccessExpiresAt = Earlier(now + lifetimes.Access, AbsoluteExpiresAt);
        RefreshExpiresAt = Earlier(now + lifetimes.Idle, AbsoluteExpiresAt);
    }

    private static DateTimeOffset Earlier(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
