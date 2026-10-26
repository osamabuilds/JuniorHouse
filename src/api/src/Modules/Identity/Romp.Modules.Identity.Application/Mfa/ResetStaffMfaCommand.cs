using MediatR;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Application.Mfa;

/// <summary>
/// An administrator clears another person's second factor, recovery codes and sessions, so they enrol again at their next sign-in
/// (mfa AC-8). The endpoint requires <c>staff.manage</c>; this command refuses a person who targets themselves, because the person who
/// lost the device must not be able to remove the check with a stolen session. The change is audited by the generic mechanism.
/// Repeating it is harmless.
/// </summary>
public sealed record ResetStaffMfaCommand(long TargetUserId, long ActingUserId) : IRequest, IIdentityCommand;

public sealed class ResetStaffMfaCommandHandler(IIdentityDbContext dbContext, IMfaEnrolmentReset reset, IUserSessionRevoker sessions)
    : IRequestHandler<ResetStaffMfaCommand>
{
    public async Task Handle(ResetStaffMfaCommand request, CancellationToken cancellationToken)
    {
        if (request.TargetUserId == request.ActingUserId)
        {
            throw new DomainException("You cannot reset your own two-step verification. Ask another administrator, or the Owner.");
        }

        _ = await StaffLookups.GetAsync(dbContext, request.TargetUserId, cancellationToken); // not found is a 404, not a silent success
        await reset.ClearAsync(request.TargetUserId, cancellationToken);
        await sessions.RevokeAsync(request.TargetUserId, exceptSessionId: null, SessionRevokeReason.MfaReset, cancellationToken);
    }
}
