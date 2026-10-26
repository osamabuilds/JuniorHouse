using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Application.Roles;

/// <summary>A role a person can be given. <paramref name="HasAllPermissions"/> marks the Owner role, which the screens can warn about.</summary>
public sealed record RoleDto(short Id, string Code, string Name, string? Description, bool HasAllPermissions);

/// <summary>The active roles in display order. A lookup of a handful of rows, so it is not paged.</summary>
public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class ListRolesQueryHandler(IIdentityDbContext dbContext) : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken) =>
        await dbContext.Roles.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortSeq).ThenBy(r => r.Id)
            .Select(r => new RoleDto(r.Id, r.Code, r.Name, r.Description, r.HasAllPermissions))
            .ToListAsync(cancellationToken);
}

/// <summary>
/// The permission codes a person holds right now (roles AC-2, AC-3): every active permission when they hold an active role flagged as
/// all-permissions (the Owner, including permissions added by later migrations), otherwise the union over their active roles through
/// the role-permission mapping. An inactive account, an account with no roles and an unknown id hold nothing. One query, nothing cached:
/// a role change or a deactivation applies on the next request.
/// </summary>
public sealed record GetEffectivePermissionsQuery(long UserId) : IRequest<IReadOnlySet<string>>;

public sealed class GetEffectivePermissionsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetEffectivePermissionsQuery, IReadOnlySet<string>>
{
    public async Task<IReadOnlySet<string>> Handle(GetEffectivePermissionsQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;
        var codes = await dbContext.Permissions.AsNoTracking()
            .Where(p => p.IsActive && dbContext.Staff.Any(u => u.Id == userId && u.IsActive && u.Roles.Any(ur =>
                dbContext.Roles.Any(r => r.Id == ur.RoleId && r.IsActive
                    && (r.HasAllPermissions || dbContext.RolePermissions.Any(g => g.RoleId == r.Id && g.PermissionId == p.Id))))))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        return codes.ToHashSet(StringComparer.Ordinal);
    }
}
