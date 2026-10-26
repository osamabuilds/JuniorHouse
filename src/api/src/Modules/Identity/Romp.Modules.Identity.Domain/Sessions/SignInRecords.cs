using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Domain.Sessions;

/// <summary>What happened to one sign-in or MFA attempt; stored through the LGIN_OTCM_LKP lookup (ids are stable).</summary>
public enum LoginOutcome : short
{
    Ok = 1,
    BadCredentials = 2,
    Inactive = 3,
    TemporaryExpired = 4,
    MfaBad = 5,
    MfaOk = 6,
    Throttled = 7,
    RefreshReuse = 8,
}

/// <summary>Which outcomes count towards the throttle (sessions AC-6). Throttled and reuse rows are neither a failure nor a success.</summary>
public static class LoginOutcomes
{
    public static readonly LoginOutcome[] Failures =
        [LoginOutcome.BadCredentials, LoginOutcome.Inactive, LoginOutcome.TemporaryExpired, LoginOutcome.MfaBad];

    /// <summary>Failures and successes: the rows the consecutive-failure count is read from.</summary>
    public static readonly LoginOutcome[] Counted =
        [LoginOutcome.Ok, LoginOutcome.MfaOk, .. Failures];

    public static bool IsSuccess(LoginOutcome outcome) => outcome is LoginOutcome.Ok or LoginOutcome.MfaOk;
}

/// <summary>
/// IDNT.LGIN_PEND: the password step passed and the MFA step is still to come (sessions plan). Short-lived; only the token hash is stored.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: a half-finished sign-in that lives for minutes and holds a token digest.")]
public sealed class PendingSignIn : Entity<long>, IAuditable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>Wrong codes allowed for one pending sign-in; the fifth ends it (mfa AC-5).</summary>
    public const int MaxFailures = 5;

    private PendingSignIn()
    {
        TokenHash = [];
        InsrBy = string.Empty;
    }

    public long UserId { get; private set; }

    public byte[] TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailureCount { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Counts a wrong code; true when that was the last one allowed, so the caller destroys this pending sign-in.</summary>
    public bool RecordFailure()
    {
        FailureCount++;
        return FailureCount >= MaxFailures;
    }

    public static (PendingSignIn Pending, SecretString Token) Start(long userId, DateTimeOffset now)
    {
        var token = SessionTokens.Generate();
        var pending = new PendingSignIn
        {
            UserId = userId,
            TokenHash = SessionTokens.Hash(token.Reveal()),
            ExpiresAt = now + Lifetime,
        };
        return (pending, token);
    }
}

/// <summary>
/// IDNT.LGIN_ATMP: one row per sign-in or MFA attempt (NFR-15). Throttle state is derived from these rows. The name is what was typed,
/// so it is bounded and sanitised by the caller; an unknown name has no user.
/// </summary>
public sealed class LoginAttempt : Entity<long>, IAuditable
{
    public const int NameMaxLength = 40;

    private LoginAttempt()
    {
        AttemptName = string.Empty;
        ClientIp = string.Empty;
        InsrBy = string.Empty;
    }

    public string AttemptName { get; private set; }

    public string ClientIp { get; private set; }

    public LoginOutcome Outcome { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public long? UserId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static LoginAttempt Record(string attemptName, string clientIp, LoginOutcome outcome, long? userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attemptName);
        ArgumentException.ThrowIfNullOrEmpty(clientIp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attemptName.Length, NameMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clientIp.Length, Session.ClientIpMaxLength);

        return new LoginAttempt
        {
            AttemptName = attemptName,
            ClientIp = clientIp,
            Outcome = outcome,
            UserId = userId,
            AttemptedAt = now,
        };
    }
}
