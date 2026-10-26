using Romp.Modules.Identity.Application.Sessions;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>
/// Until the mfa feature lands nobody has a second factor, so every account is told to enrol: the safe default (nobody gets a full
/// session without one). The mfa feature replaces this registration.
/// </summary>
internal sealed class NoMfaYetStatus : IMfaEnrolmentStatus
{
    public Task<bool> IsEnrolledAsync(long userId, CancellationToken cancellationToken) => Task.FromResult(false);
}
