using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>What happened to one customer sign-in attempt; stored through the LGIN_OTCM_LKP lookup (ids are stable).</summary>
public enum LoginOutcome : short
{
    Ok = 1,
    BadCredentials = 2,
    Throttled = 3,
    CaptchaFailed = 4,
    RefreshReuse = 5,
}

/// <summary>Which outcomes count towards the throttle (ACT-9). Throttled and reuse rows are neither a failure nor a success.</summary>
public static class LoginOutcomes
{
    public static readonly LoginOutcome[] Failures = [LoginOutcome.BadCredentials, LoginOutcome.CaptchaFailed];

    /// <summary>Failures and successes: the rows the consecutive-failure count is read from.</summary>
    public static readonly LoginOutcome[] Counted = [LoginOutcome.Ok, .. Failures];

    public static bool IsSuccess(LoginOutcome outcome) => outcome is LoginOutcome.Ok;
}

/// <summary>
/// CUST.CUST_LGIN_ATMP: one row per sign-in attempt (ACT-10, NFR-15). The email is kept as typed, with control characters removed and
/// truncated, so a probe is recognisable without storing a forged or hostile line. The password is never recorded.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: this table is itself the sign-in record; rows are append-only.")]
public sealed class LoginAttempt : Entity<long>, IAuditable
{
    public const int EmailMaxLength = 100;

    private LoginAttempt()
    {
        AttemptedEmail = string.Empty;
        ClientIp = string.Empty;
        InsrBy = string.Empty;
    }

    public string AttemptedEmail { get; private set; }

    public string ClientIp { get; private set; }

    public LoginOutcome Outcome { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public long? CustomerId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static LoginAttempt Record(string? typedEmail, string? clientIp, LoginOutcome outcome, DateTimeOffset now, long? customerId = null)
    {
        var email = new string((typedEmail ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        var ip = (clientIp ?? string.Empty).Trim();
        return new LoginAttempt
        {
            AttemptedEmail = email.Length > EmailMaxLength ? email[..EmailMaxLength] : email,
            ClientIp = ip.Length > CustomerSession.ClientIpMaxLength ? ip[..CustomerSession.ClientIpMaxLength] : ip,
            Outcome = outcome,
            AttemptedAt = now,
            CustomerId = customerId,
        };
    }
}
