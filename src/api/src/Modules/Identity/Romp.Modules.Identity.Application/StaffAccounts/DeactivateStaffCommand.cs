using FluentValidation;
using MediatR;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>
/// An administrator deactivates an account: it cannot sign in and its sessions end now (AC-8). Idempotent. The last active Owner is
/// refused (AC-9); step 7 makes that rule hold under concurrency.
/// </summary>
public sealed record DeactivateStaffCommand(long UserId) : IRequest, IIdentityCommand;

public sealed class DeactivateStaffCommandValidator : AbstractValidator<DeactivateStaffCommand>
{
    public DeactivateStaffCommandValidator()
    {
        RuleFor(c => c.UserId).GreaterThan(0);
    }
}

public sealed class DeactivateStaffCommandHandler(IIdentityDbContext dbContext, IUserSessionRevoker sessions) : IRequestHandler<DeactivateStaffCommand>
{
    public async Task Handle(DeactivateStaffCommand request, CancellationToken cancellationToken)
    {
        var user = await StaffLookups.GetAsync(dbContext, request.UserId, cancellationToken);

        user.Deactivate(await StaffLookups.OwnerGuardForAsync(dbContext, user, cancellationToken));
        await sessions.RevokeAsync(user.Id, exceptSessionId: null, cancellationToken);
    }
}
