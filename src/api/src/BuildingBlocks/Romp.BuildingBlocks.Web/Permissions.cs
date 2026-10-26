using System.Reflection;
using System.Text.RegularExpressions;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// A permission code an endpoint declares (<c>area.action</c>, lower-case, at most 30 characters so it fits the lookup
/// <c>CODE</c> column). A distinct type, not a bare string, so a permission can never be passed where a legacy policy name is
/// expected (and the other way round): <c>MapSecuredGroup(prefix, Permissions.CatalogRead)</c> and
/// <c>MapSecuredGroup(prefix, Policies.Public)</c> are different overloads.
/// </summary>
public readonly partial record struct PermissionCode
{
    public PermissionCode(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 30 || !CodePattern().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a valid permission code (area.action, lower-case, at most 30 characters).", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z]+\.[a-z]+$")]
    private static partial Regex CodePattern();
}

/// <summary>
/// The permission codes of the admin API (Sprint 8, roles spec "Permission catalogue"). Endpoints name the code they need; which role
/// holds which code is lookup data (<c>IDNT.PERM_LKP</c>, <c>ROLE_PERM_MAP</c>), never code (SEC-08, FR-60). A route group is declared
/// with the read permission and a mutating route adds the write permission, so a role holding a write code also holds its read code.
/// </summary>
public static class Permissions
{
    public static readonly PermissionCode RefRead = new("ref.read");
    public static readonly PermissionCode RefWrite = new("ref.write");
    public static readonly PermissionCode CatalogRead = new("catalog.read");
    public static readonly PermissionCode CatalogWrite = new("catalog.write");
    public static readonly PermissionCode ProcurementRead = new("procurement.read");
    public static readonly PermissionCode ProcurementWrite = new("procurement.write");
    public static readonly PermissionCode ProductionRead = new("production.read");
    public static readonly PermissionCode ProductionWrite = new("production.write");
    public static readonly PermissionCode QcRead = new("qc.read");
    public static readonly PermissionCode QcWrite = new("qc.write");
    public static readonly PermissionCode LabellingRead = new("labelling.read");
    public static readonly PermissionCode LabellingWrite = new("labelling.write");
    public static readonly PermissionCode WarehouseRead = new("warehouse.read");
    public static readonly PermissionCode WarehouseWrite = new("warehouse.write");
    public static readonly PermissionCode InventoryRead = new("inventory.read");
    public static readonly PermissionCode InventoryWrite = new("inventory.write");
    public static readonly PermissionCode OpsAdmin = new("ops.admin");
    public static readonly PermissionCode StaffManage = new("staff.manage");
    public static readonly PermissionCode AuditRead = new("audit.read");

    /// <summary>Every declared code, for the seed test (<c>PERM_LKP</c> must hold exactly these) and the authorization matrix.</summary>
    public static IReadOnlyList<PermissionCode> All { get; } =
        typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PermissionCode))
            .Select(f => (PermissionCode)f.GetValue(null)!)
            .ToArray();
}
