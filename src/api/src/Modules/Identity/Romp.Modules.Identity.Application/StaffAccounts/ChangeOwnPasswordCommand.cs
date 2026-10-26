using FluentValidation;
using MediatR;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>
/// A person changes their own password (AC-7, AC-2). The current password is required (after a reset or first sign-in that is the
/// temporary one), the new one must pass the policy and differ from the last three, and every other session ends.
/// <paramref name="SessionId"/> is the caller's own session, which stays signed in.
/// </summary>
public sealed record ChangeOwnPasswordCommand(long UserId, long? SessionId, SecretString CurrentPassword, SecretString NewPassword)
    : IRequest, IIdentityCommand;

public sealed class ChangeOwnPasswordCommandValidator : AbstractValidator<ChangeOwnPasswordCommand>
{
    public ChangeOwnPasswordCommandValidator()
    {
        RuleFor(c => c.UserId).GreaterThan(0);
        RuleFor(c => c.CurrentPassword).NotNull();
        RuleFor(c => c.NewPassword).NotNull();
    }
}

public sealed class ChangeOwnPasswordCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher hasher,
    PasswordPolicy policy,
    IUserSessionRevoker sessions) : IRequestHandler<ChangeOwnPasswordCommand>
{
    public async Task Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await StaffLookups.GetAsync(dbContext, request.UserId, cancellationToken);

        if (!user.IsActive || !hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("The current password is not correct.");
        }

        StaffLookups.EnsurePasswordAcceptable(policy, request.NewPassword, user.UserName, nameof(request.NewPassword));

        user.SetPassword(hasher.Hash(request.NewPassword), stored => hasher.Verify(request.NewPassword, stored));
        await sessions.RevokeAsync(user.Id, request.SessionId, cancellationToken);
    }
}
