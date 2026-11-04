using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>
/// How long a session's parts live (ACT-12). Access value, idle window and absolute limit. "Keep me signed in" (Q-ACT-4) stretches the idle
/// window and the absolute limit to 30 days; the access value stays short either way.
/// </summary>
public sealed record CustomerSessionLifetimes
{
    public static readonly CustomerSessionLifetimes Standard = new(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(12), keepSignedIn: false);

    public static readonly CustomerSessionLifetimes KeepSignedIn = new(TimeSpan.FromMinutes(15), TimeSpan.FromDays(30), TimeSpan.FromDays(30), keepSignedIn: true);

    public CustomerSessionLifetimes(TimeSpan access, TimeSpan idle, TimeSpan absolute, bool keepSignedIn)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(access, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(idle, access);
        ArgumentOutOfRangeException.ThrowIfLessThan(absolute, idle);
        Access = access;
        Idle = idle;
        Absolute = absolute;
        IsKeepSignedIn = keepSignedIn;
    }

    public TimeSpan Access { get; }

    public TimeSpan Idle { get; }

    public TimeSpan Absolute { get; }

    public bool IsKeepSignedIn { get; }

    public static CustomerSessionLifetimes For(bool keepSignedIn) => keepSignedIn ? KeepSignedIn : Standard;
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

    /// <summary>
    /// The presented value was rotated away less than <see cref="CustomerSession.RefreshGrace"/> ago, so it is most likely a second request
    /// that carried the same cookie. Refused with no new pair and no revocation; the client retries with the cookie the winner set.
    /// </summary>
    ConcurrentRefresh = 4,
}

public sealed record RefreshResult(RefreshOutcome Outcome, IssuedTokens? Tokens);

/// <summary>Why a session ended; stored through a lookup (ids are stable).</summary>
public enum CustomerSessionRevokeReason : short
{
    SignOut = 1,
    SignOutEverywhere = 2,
    RefreshReuse = 3,
    NewSignIn = 4,
    PasswordChanged = 5,
    SessionLimit = 6,
}

/// <summary>
/// CUST.CUST_SESN: a signed-in customer's session (ACT-11, ACT-12). Holds the hashes of the access and refresh values, never the values.
/// A refresh value is single use: presenting the previous one again means it leaked, so the whole session is revoked.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: a session changes on every refresh; its start, end and revocation reason are on the sign-in attempts.")]
public sealed class CustomerSession : AggregateRoot<long>, IAuditable
{
    public const int MaxPerCustomer = 10;

    /// <summary>How long after a rotation the previous refresh value is treated as a concurrent retry rather than as theft.</summary>
    public static readonly TimeSpan RefreshGrace = TimeSpan.FromSeconds(10);
    public const int ClientIpMaxLength = 45;
    public const int UserAgentMaxLength = 200;

    private CustomerSession()
    {
        AccessTokenHash = [];
        RefreshTokenHash = [];
        InsrBy = string.Empty;
    }

    public long CustomerId { get; private set; }

    public byte[] AccessTokenHash { get; private set; }

    public byte[] RefreshTokenHash { get; private set; }

    public byte[]? PreviousRefreshTokenHash { get; private set; }

    public bool KeepSignedIn { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset AccessExpiresAt { get; private set; }

    public DateTimeOffset RefreshExpiresAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    /// <summary>When the refresh value last changed; null until the first rotation. Starts the <see cref="RefreshGrace"/> window.</summary>
    public DateTimeOffset? RotatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public CustomerSessionRevokeReason? RevokeReason { get; private set; }

    public string? ClientIp { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>
    /// Starts a session. The client address and browser text are kept only to help the customer recognise a session; the agent text is
    /// truncated, never rejected.
    /// </summary>
    public static (CustomerSession Session, IssuedTokens Tokens) Start(
        long customerId, DateTimeOffset now, CustomerSessionLifetimes lifetimes, string? clientIp = null, string? userAgent = null)
    {
        ArgumentNullException.ThrowIfNull(lifetimes);

        var tokens = new IssuedTokens(CustomerTokens.Generate(), CustomerTokens.Generate());
        var session = new CustomerSession
        {
            CustomerId = customerId,
            KeepSignedIn = lifetimes.IsKeepSignedIn,
            StartedAt = now,
            AbsoluteExpiresAt = now + lifetimes.Absolute,
            ClientIp = clientIp is { Length: > ClientIpMaxLength } ? clientIp[..ClientIpMaxLength] : clientIp,
            UserAgent = userAgent is { Length: > UserAgentMaxLength } ? userAgent[..UserAgentMaxLength] : userAgent,
        };
        session.Apply(tokens, now, lifetimes);
        return (session, tokens);
    }

    /// <summary>Not revoked, inside the absolute limit and inside the idle window: the session can still be refreshed.</summary>
    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && now < AbsoluteExpiresAt && now < RefreshExpiresAt;

    /// <summary>Active and the short-lived access value has not expired.</summary>
    public bool AccessIsValid(DateTimeOffset now) => IsActive(now) && now < AccessExpiresAt;

    public bool AccessTokenMatches(string? presented) => CustomerTokens.Matches(presented, AccessTokenHash);

    /// <summary>
    /// Exchanges the current refresh value for a new pair, with the lifetimes the session started with. Idle and access expiry never run
    /// past the absolute limit.
    /// </summary>
    public RefreshResult Rotate(string? presentedRefresh, DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            return new RefreshResult(RefreshOutcome.Unusable, null);
        }

        if (CustomerTokens.Matches(presentedRefresh, RefreshTokenHash))
        {
            var tokens = new IssuedTokens(CustomerTokens.Generate(), CustomerTokens.Generate());
            PreviousRefreshTokenHash = RefreshTokenHash;
            RotatedAt = now;
            Apply(tokens, now, CustomerSessionLifetimes.For(KeepSignedIn));
            return new RefreshResult(RefreshOutcome.Rotated, tokens);
        }

        if (PreviousRefreshTokenHash is not null && CustomerTokens.Matches(presentedRefresh, PreviousRefreshTokenHash))
        {
            if (RotatedAt is { } rotatedAt && now - rotatedAt < RefreshGrace)
            {
                return new RefreshResult(RefreshOutcome.ConcurrentRefresh, null);
            }

            Revoke(CustomerSessionRevokeReason.RefreshReuse, now);
            return new RefreshResult(RefreshOutcome.ReuseDetected, null);
        }

        return new RefreshResult(RefreshOutcome.Unusable, null);
    }

    /// <summary>Ends the session. Final: a second call keeps the first date and reason.</summary>
    public void Revoke(CustomerSessionRevokeReason reason, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokeReason = reason;
    }

    /// <summary>
    /// The sessions to revoke so that starting one more keeps the customer within <see cref="MaxPerCustomer"/>: the oldest active ones.
    /// Revoked and expired sessions do not count.
    /// </summary>
    public static IReadOnlyList<CustomerSession> SelectToEvict(IReadOnlyCollection<CustomerSession> customerSessions, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(customerSessions);

        var active = customerSessions.Where(s => s.IsActive(now)).OrderBy(s => s.StartedAt).ToList();
        var excess = active.Count - (MaxPerCustomer - 1);
        return excess <= 0 ? [] : active.Take(excess).ToList();
    }

    private void Apply(IssuedTokens tokens, DateTimeOffset now, CustomerSessionLifetimes lifetimes)
    {
        AccessTokenHash = CustomerTokens.Hash(tokens.Access.Reveal());
        RefreshTokenHash = CustomerTokens.Hash(tokens.Refresh.Reveal());
        AccessExpiresAt = Earlier(now + lifetimes.Access, AbsoluteExpiresAt);
        RefreshExpiresAt = Earlier(now + lifetimes.Idle, AbsoluteExpiresAt);
    }

    private static DateTimeOffset Earlier(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
