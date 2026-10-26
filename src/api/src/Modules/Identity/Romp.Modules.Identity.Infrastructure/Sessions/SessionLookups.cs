using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>IDNT.SESS_RVKD_RSN_LKP row. The key is the enum so the foreign key from SESS_MAIN needs no conversion.</summary>
internal sealed class RevokeReasonRow
{
    public SessionRevokeReason Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>IDNT.LGIN_OTCM_LKP row.</summary>
internal sealed class LoginOutcomeRow
{
    public LoginOutcome Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Seed rows of the two session lookups, written into the <c>AddSessions</c> migration. Ids are the enum values and are never reused;
/// <c>SessionMigrationTests</c> proves every enum value has a row.
/// </summary>
internal static class SessionSeed
{
    public static IReadOnlyList<RevokeReasonRow> RevokeReasons { get; } =
    [
        Reason(SessionRevokeReason.SignOut, "SIGN_OUT", "Signed out"),
        Reason(SessionRevokeReason.SignOutEverywhere, "SIGN_OUT_ALL", "Signed out everywhere"),
        Reason(SessionRevokeReason.RefreshReuse, "REFRESH_REUSE", "Rotated refresh value presented again"),
        Reason(SessionRevokeReason.NewSignIn, "NEW_SIGN_IN", "Replaced by a new sign-in"),
        Reason(SessionRevokeReason.PasswordChanged, "PWD_CHNG", "Password changed or reset"),
        Reason(SessionRevokeReason.AccountDeactivated, "DEACT", "Account deactivated"),
        Reason(SessionRevokeReason.AdminRevoked, "ADMIN", "Revoked by an administrator"),
        Reason(SessionRevokeReason.SessionLimit, "SESS_LIMIT", "Oldest session over the per-user limit"),
        Reason(SessionRevokeReason.MfaReset, "MFA_RESET", "Multi-factor enrolment reset"),
    ];

    public static IReadOnlyList<LoginOutcomeRow> LoginOutcomes { get; } =
    [
        Outcome(LoginOutcome.Ok, "OK", "Password accepted"),
        Outcome(LoginOutcome.BadCredentials, "BAD_CRED", "Unknown user or wrong password"),
        Outcome(LoginOutcome.Inactive, "INACTIVE", "Account inactive"),
        Outcome(LoginOutcome.TemporaryExpired, "TEMP_EXPD", "Temporary password expired"),
        Outcome(LoginOutcome.MfaBad, "MFA_BAD", "Wrong multi-factor code"),
        Outcome(LoginOutcome.MfaOk, "MFA_OK", "Multi-factor code accepted"),
        Outcome(LoginOutcome.Throttled, "THROTTLED", "Refused while a delay was running"),
        Outcome(LoginOutcome.RefreshReuse, "REFRESH_REUSE", "Rotated refresh value presented again"),
    ];

    private static RevokeReasonRow Reason(SessionRevokeReason id, string code, string name) =>
        new() { Id = id, Code = code, Name = name, SortSeq = (short)id, IsActive = true };

    private static LoginOutcomeRow Outcome(LoginOutcome id, string code, string name) =>
        new() { Id = id, Code = code, Name = name, SortSeq = (short)id, IsActive = true };
}
