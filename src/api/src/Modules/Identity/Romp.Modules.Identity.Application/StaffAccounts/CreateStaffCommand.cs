using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>An administrator adds an account with a generated password that works once (AC-4, AC-10, AC-11). The password is returned this once.</summary>
public sealed record CreateStaffCommand(string UserName, string DisplayName, IReadOnlyList<short> RoleIds)
    : IRequest<TemporaryPasswordResult>, IIdentityCommand;

public sealed class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffCommandValidator()
    {
        // The charset lives in UserName.Create (one source of truth); the validator turns a bad name into a 400 with a field error.
        RuleFor(c => c.UserName).NotEmpty().Must(StaffLookups.IsValidUserName)
            .WithMessage(StaffLookups.UserNameRule);
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(StaffUser.DisplayNameMaxLength)
            .Must(name => !name.Any(char.IsControl)).WithMessage("The display name must not contain control characters.");
        RuleFor(c => c.RoleIds).NotNull().Must(ids => ids is { Count: <= 20 }).WithMessage("Give at most 20 roles.");
    }
}

public sealed class CreateStaffCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher hasher,
    ITemporaryPasswordGenerator generator,
    TimeProvider clock) : IRequestHandler<CreateStaffCommand, TemporaryPasswordResult>
{
    public async Task<TemporaryPasswordResult> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        var userName = Domain.StaffAccounts.UserName.Create(request.UserName);

        if (await dbContext.Staff.AnyAsync(u => u.UserName == userName.Value, cancellationToken))
        {
            throw new DomainException($"The user name '{userName.Value}' is already taken.");
        }

        await StaffLookups.EnsureRolesExistAsync(dbContext, request.RoleIds, cancellationToken);

        var temporary = generator.Generate();
        var user = StaffUser.CreateWithTemporaryPassword(
            userName, request.DisplayName, hasher.Hash(temporary), request.RoleIds, clock.GetUtcNow());

        dbContext.Staff.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TemporaryPasswordResult(user.Id, user.UserName, temporary, user.TemporaryPasswordExpiresAt!.Value);
    }
}
