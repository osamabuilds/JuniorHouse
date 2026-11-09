using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.StaffAccounts;

namespace Romp.Modules.Identity.Application.Roles;

/// <summary>
/// An administrator replaces a person's roles (roles AC-6). Repeating it gives the same state. The change applies on the person's next
/// request, because permissions are read from the database every time. The last active Owner cannot lose the Owner role, and a person
/// holds at most <see cref="MaxRoles"/> roles.
/// </summary>
public sealed record AssignRolesCommand(long UserId, IReadOnlyCollection<short> RoleIds) : IRequest, IIdentityCommand
{
    public const int MaxRoles = 5;
}

public sealed class AssignRolesCommandValidator : AbstractValidator<AssignRolesCommand>
{
    public AssignRolesCommandValidator()
    {
        RuleFor(c => c.UserId).GreaterThan(0);
        RuleFor(c => c.RoleIds).NotNull().Must(ids => ids is null || ids.Count <= AssignRolesCommand.MaxRoles)
            .WithMessage($"A person can hold at most {AssignRolesCommand.MaxRoles} roles.");
        RuleForEach(c => c.RoleIds).GreaterThan((short)0);
    }
}

public sealed class AssignRolesCommandHandler(IIdentityDbContext dbContext) : IRequestHandler<AssignRolesCommand>
{
    public async Task Handle(AssignRolesCommand request, CancellationToken cancellationToken)
    {
        // Lock first, then read: two changes that could each remove the last Owner are taken one at a time (AC-6 under concurrency).
        await dbContext.LockOwnersAsync(cancellationToken);
        var user = await StaffLookups.GetAsync(dbContext, request.UserId, cancellationToken);
        await StaffLookups.EnsureRolesExistAsync(dbContext, request.RoleIds, cancellationToken);

        user.SetRoles(request.RoleIds, await StaffLookups.OwnerGuardForAsync(dbContext, user, cancellationToken));
    }
}

/// <summary>A permission as the matrix page shows it.</summary>
public sealed record PermissionDto(string Code, string Name, string? Description);

/// <summary>A role with the permission codes it holds; for the Owner (the all-permissions flag) that is every active permission.</summary>
public sealed record RoleWithPermissionsDto(short Id, string Code, string Name, string? Description, bool HasAllPermissions, IReadOnlyList<string> PermissionCodes);

public sealed record RoleMatrixDto(IReadOnlyList<PermissionDto> Permissions, IReadOnlyList<RoleWithPermissionsDto> Roles);

/// <summary>The read-only role and permission matrix (roles AC-6): two queries however many roles there are, no N+1.</summary>
public sealed record GetRoleMatrixQuery : IRequest<RoleMatrixDto>;

public sealed class GetRoleMatrixQueryHandler(IIdentityDbContext dbContext) : IRequestHandler<GetRoleMatrixQuery, RoleMatrixDto>
{
    public async Task<RoleMatrixDto> Handle(GetRoleMatrixQuery request, CancellationToken cancellationToken)
    {
        var permissions = await dbContext.Permissions.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortSeq).ThenBy(p => p.Id)
            .Select(p => new PermissionDto(p.Code, p.Name, p.Description))
            .ToListAsync(cancellationToken);

        var roles = await dbContext.Roles.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortSeq).ThenBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Name,
                r.Description,
                r.HasAllPermissions,
                Codes = dbContext.RolePermissions
                    .Where(g => g.RoleId == r.Id && g.Permission!.IsActive)
                    .Select(g => g.Permission!.Code)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var everything = permissions.Select(p => p.Code).ToList();
        return new RoleMatrixDto(
            permissions,
            roles.Select(r => new RoleWithPermissionsDto(
                r.Id, r.Code, r.Name, r.Description, r.HasAllPermissions, r.HasAllPermissions ? everything : r.Codes.Order(StringComparer.Ordinal).ToList())).ToList());
    }
}
