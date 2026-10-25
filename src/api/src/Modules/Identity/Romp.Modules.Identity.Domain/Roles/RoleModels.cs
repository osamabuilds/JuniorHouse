using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Identity.Domain.Roles;

/// <summary>The stable role codes code may refer to (the rows are seeded by migration; roles spec "Permission catalogue").</summary>
public static class RoleCodes
{
    public const string Owner = "OWNER";
    public const string Catalogue = "CATALOGUE";
    public const string OrderOps = "ORDER_OPS";
    public const string Support = "SUPPORT";
    public const string Marketing = "MARKETING";
}

/// <summary>IDNT.ROLE_LKP: a named set of permissions. The Owner role carries <see cref="HasAllPermissions"/> instead of a copied list, so permissions added later need no mapping row.</summary>
public sealed class Role : Entity<short>
{
    private Role()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public short SortSeq { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool HasAllPermissions { get; private set; }
}

/// <summary>IDNT.PERM_LKP: one permission code (<c>area.action</c>) that an endpoint can require.</summary>
public sealed class PermissionLookup : Entity<short>
{
    private PermissionLookup()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public short SortSeq { get; private set; }

    public bool IsActive { get; private set; } = true;
}

/// <summary>IDNT.ROLE_PERM_MAP: a role holds a permission. Keyed by the pair; there is no row for the Owner role.</summary>
public sealed class RoleGrant
{
    private RoleGrant()
    {
    }

    public short RoleId { get; private set; }

    public short PermissionId { get; private set; }

    public Role? Role { get; private set; }

    public PermissionLookup? Permission { get; private set; }
}
