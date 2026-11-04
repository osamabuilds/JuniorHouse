using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>
/// Ends sign-in sessions. Declared here and implemented by the sessions feature of this module (an in-module port, staff-accounts
/// plan), so a deactivation or a password change takes effect on the next request (AC-7, AC-8).
/// </summary>
public interface IUserSessionRevoker
{
    /// <summary>Revokes every active session of the account, except <paramref name="exceptSessionId"/> when one is given; the caller saves.</summary>
    Task RevokeAsync(long userId, long? exceptSessionId, SessionRevokeReason reason, CancellationToken cancellationToken);
}

/// <summary>
/// The one time an account's generated password is visible (AC-4). Holds a <see cref="SecretString"/>, so it cannot be serialised or
/// logged by mistake; the endpoint reveals it into its own response.
/// </summary>
public sealed record TemporaryPasswordResult(long UserId, string UserName, SecretString TemporaryPassword, DateTimeOffset ExpiresAt);
