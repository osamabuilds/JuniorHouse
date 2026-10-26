namespace Romp.Modules.Identity.Domain.Sessions;

/// <summary>
/// What a session may do (sessions plan). Computed on every request from the account, never stored, so finishing a step takes effect
/// at once and an old session cannot keep a scope the account no longer has.
/// </summary>
public enum SessionScope
{
    /// <summary>Everything the person's roles allow.</summary>
    Full = 1,

    /// <summary>Only enrolling a second factor.</summary>
    EnrolMfa = 2,

    /// <summary>Only choosing a new password.</summary>
    ChangePassword = 3,
}

public static class SessionScopes
{
    /// <summary>The password comes first; enrolment follows. Only an account with neither outstanding gets <see cref="SessionScope.Full"/>.</summary>
    public static SessionScope For(bool mustChangePassword, bool mfaEnrolled) =>
        mustChangePassword ? SessionScope.ChangePassword
        : mfaEnrolled ? SessionScope.Full
        : SessionScope.EnrolMfa;
}
