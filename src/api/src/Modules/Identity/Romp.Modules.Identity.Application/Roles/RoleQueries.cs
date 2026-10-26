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
