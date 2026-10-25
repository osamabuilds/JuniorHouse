using FluentValidation;
using MediatR;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>An administrator gives an account a new generated password and ends its sessions (AC-4, AC-8). Refused for a deactivated account.</summary>
public sealed record ResetPasswordCommand(long UserId) : IRequest<TemporaryPasswordResult>, IIdentityCommand;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.UserId).GreaterThan(0);
    }
}

public sealed class ResetPasswordCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher hasher,
    ITemporaryPasswordGenerator generator,
    IUserSessionRevoker sessions,
    TimeProvider clock) : IRequestHandler<ResetPasswordCommand, TemporaryPasswordResult>
{
    public async Task<TemporaryPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await StaffLookups.GetAsync(dbContext, request.UserId, cancellationToken);

        var temporary = generator.Generate();
        user.IssueTemporaryPassword(hasher.Hash(temporary), clock.GetUtcNow());
        await sessions.RevokeAsync(user.Id, exceptSessionId: null, cancellationToken);

        return new TemporaryPasswordResult(user.Id, user.UserName, temporary, user.TemporaryPasswordExpiresAt!.Value);
    }
}
