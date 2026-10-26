using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;
using ValidationException = Romp.BuildingBlocks.Application.ValidationException;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>
/// Clears an account's multi-factor enrolment so it must enrol again at the next sign-in. Declared here and implemented by the mfa
/// feature of this module; until then there is nothing enrolled to clear (AC-13).
/// </summary>
public interface IMfaEnrolmentReset
{
    Task ClearAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>
/// The operator's one-time bootstrap (AC-1): creates the first Owner with a password they supply. Refused, changing nothing, once
/// any account holds the Owner role. The Owner must still change the password at the first sign-in.
/// </summary>
public sealed record CreateOwnerCommand(string UserName, string DisplayName, SecretString Password) : IRequest<long>, IIdentityCommand;

public sealed class CreateOwnerCommandValidator : AbstractValidator<CreateOwnerCommand>
{
    public CreateOwnerCommandValidator()
    {
        RuleFor(c => c.UserName).NotEmpty().Must(StaffLookups.IsValidUserName).WithMessage(StaffLookups.UserNameRule);
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(StaffUser.DisplayNameMaxLength);
        RuleFor(c => c.Password).NotNull();
    }
}

public sealed class CreateOwnerCommandHandler(IIdentityDbContext dbContext, IPasswordHasher hasher, PasswordPolicy policy)
    : IRequestHandler<CreateOwnerCommand, long>
{
    public async Task<long> Handle(CreateOwnerCommand request, CancellationToken cancellationToken)
    {
        var userName = Domain.StaffAccounts.UserName.Create(request.UserName);
        StaffLookups.EnsurePasswordAcceptable(policy, request.Password, userName.Value, nameof(request.Password));

        // The lock serialises two operators running this at once: the second one sees the first one's Owner.
        await dbContext.LockOwnersAsync(cancellationToken);
        var ownerRoleId = await dbContext.Roles.Where(r => r.Code == RoleCodes.Owner).Select(r => r.Id).SingleAsync(cancellationToken);
        if (await dbContext.Staff.AnyAsync(u => u.Roles.Any(r => r.RoleId == ownerRoleId), cancellationToken))
        {
            throw new DomainException("An Owner account already exists. This step only creates the first one.");
        }

        if (await dbContext.Staff.AnyAsync(u => u.UserName == userName.Value, cancellationToken))
        {
            throw new DomainException($"The user name '{userName.Value}' is already taken.");
        }

        var owner = StaffUser.CreateWithPassword(userName, request.DisplayName, hasher.Hash(request.Password), [ownerRoleId]);
        dbContext.Staff.Add(owner);
        await dbContext.SaveChangesAsync(cancellationToken);
        return owner.Id;
    }
}

/// <summary>
/// Break-glass (AC-13): the operator resets an Owner's password and/or clears the Owner's MFA enrolment, and every session of the
/// account ends. Refused for any account that is not an Owner.
/// </summary>
public sealed record ResetOwnerCommand(string UserName, SecretString? NewPassword, bool ClearMfa) : IRequest, IIdentityCommand;

public sealed class ResetOwnerCommandValidator : AbstractValidator<ResetOwnerCommand>
{
    public ResetOwnerCommandValidator()
    {
        RuleFor(c => c.UserName).NotEmpty().MaximumLength(Domain.StaffAccounts.UserName.MaxLength);
        RuleFor(c => c).Must(c => c.NewPassword is not null || c.ClearMfa).WithName("Request").WithMessage("Choose a new password, clearing MFA, or both.");
    }
}

public sealed class ResetOwnerCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher hasher,
    PasswordPolicy policy,
    IUserSessionRevoker sessions,
    IMfaEnrolmentReset mfa) : IRequestHandler<ResetOwnerCommand>
{
    public async Task Handle(ResetOwnerCommand request, CancellationToken cancellationToken)
    {
        var name = Domain.StaffAccounts.UserName.Create(request.UserName).Value;
        var user = await dbContext.Staff.Include(u => u.Roles).Include(u => u.PasswordHistory)
                .FirstOrDefaultAsync(u => u.UserName == name, cancellationToken)
            ?? throw new KeyNotFoundException("No account has that user name.");

        var ownerRoleId = await dbContext.Roles.Where(r => r.Code == RoleCodes.Owner).Select(r => r.Id).SingleAsync(cancellationToken);
        if (!user.RoleIds.Contains(ownerRoleId))
        {
            throw new DomainException("That account is not an Owner. This step only resets Owner accounts.");
        }

        if (request.NewPassword is not null)
        {
            StaffLookups.EnsurePasswordAcceptable(policy, request.NewPassword, user.UserName, nameof(request.NewPassword));
            user.ResetPasswordByOperator(hasher.Hash(request.NewPassword));
        }

        if (request.ClearMfa)
        {
            await mfa.ClearAsync(user.Id, cancellationToken);
        }

        await sessions.RevokeAsync(user.Id, exceptSessionId: null, cancellationToken);
    }
}
