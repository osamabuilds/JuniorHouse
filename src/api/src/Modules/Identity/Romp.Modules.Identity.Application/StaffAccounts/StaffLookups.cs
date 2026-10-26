using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>What the staff handlers share: loading an account, and reading the Owner facts the aggregate cannot see (AC-9).</summary>
internal static class StaffLookups
{
    public static readonly string UserNameRule =
        $"A user name is {Domain.StaffAccounts.UserName.MinLength} to {Domain.StaffAccounts.UserName.MaxLength} characters of lower-case letters, digits, '.', '_' and '-'.";

    /// <summary>The charset lives in <c>UserName.Create</c> (one source of truth); this turns a bad name into a yes/no for validators.</summary>
    public static bool IsValidUserName(string? raw)
    {
        try
        {
            _ = Domain.StaffAccounts.UserName.Create(raw ?? string.Empty);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    /// <summary>A password that fails the policy becomes a validation error that says why and never repeats the password (AC-2).</summary>
    public static void EnsurePasswordAcceptable(PasswordPolicy policy, SecretString password, string userName, string field)
    {
        var verdict = policy.Evaluate(password.Reveal(), userName);
        if (!verdict.IsAcceptable)
        {
            var reasons = verdict.Failures.Select(PasswordPolicyMessages.For).ToArray();
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]> { [field] = reasons });
        }
    }

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
