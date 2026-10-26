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

/// <summary>
/// IDNT.LGIN_PEND: the password step passed and the MFA step is still to come (sessions plan). Short-lived; only the token hash is stored.
/// </summary>
public sealed class PendingSignIn : Entity<long>, IAuditable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

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
