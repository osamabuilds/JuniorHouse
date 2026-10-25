using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>What the staff handlers share: loading an account, and reading the Owner facts the aggregate cannot see (AC-9).</summary>
internal static class StaffLookups
{
    public static async Task<StaffUser> GetAsync(IIdentityDbContext db, long userId, CancellationToken cancellationToken) =>
        await db.Staff.Include(u => u.Roles).Include(u => u.PasswordHistory).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new KeyNotFoundException($"Staff account {userId} was not found.");

    public static async Task<OwnerGuard> OwnerGuardForAsync(IIdentityDbContext db, StaffUser user, CancellationToken cancellationToken)
    {
        var ownerRoleId = await db.Roles.Where(r => r.Code == RoleCodes.Owner).Select(r => r.Id).SingleAsync(cancellationToken);
        var otherOwner = await db.Staff.AnyAsync(
            u => u.Id != user.Id && u.IsActive && u.Roles.Any(r => r.RoleId == ownerRoleId), cancellationToken);
        return new OwnerGuard(ownerRoleId, otherOwner);
    }

    /// <summary>Every id must be an active role, so a request cannot grant a role that does not exist.</summary>
    public static async Task EnsureRolesExistAsync(IIdentityDbContext db, IReadOnlyCollection<short> roleIds, CancellationToken cancellationToken)
    {
        var distinct = roleIds.Distinct().ToArray();
        var found = await db.Roles.CountAsync(r => distinct.Contains(r.Id) && r.IsActive, cancellationToken);
        if (found != distinct.Length)
        {
            throw new DomainException("One of the roles does not exist.");
        }
    }
}
