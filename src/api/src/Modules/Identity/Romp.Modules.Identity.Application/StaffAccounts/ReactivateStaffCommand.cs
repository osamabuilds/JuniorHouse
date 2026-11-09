using FluentValidation;
using MediatR;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>An administrator turns a deactivated account back on. Idempotent; the password is unchanged, so use a reset to hand over a new one.</summary>
public sealed record ReactivateStaffCommand(long UserId) : IRequest, IIdentityCommand;

public sealed class ReactivateStaffCommandValidator : AbstractValidator<ReactivateStaffCommand>
{
    public ReactivateStaffCommandValidator()
    {
        RuleFor(c => c.UserId).GreaterThan(0);
    }
}

public sealed class ReactivateStaffCommandHandler(IIdentityDbContext dbContext) : IRequestHandler<ReactivateStaffCommand>
{
    public async Task Handle(ReactivateStaffCommand request, CancellationToken cancellationToken)
    {
        var user = await StaffLookups.GetAsync(dbContext, request.UserId, cancellationToken);
        user.Reactivate();
    }
}
