using Romp.Modules.Identity.Domain.Roles;

namespace Romp.Modules.Identity.Infrastructure.Roles;

/// <summary>
/// The seed rows of the roles spec ("Permission catalogue"), written into the <c>AddIdentityLookups</c> migration. Ids are fixed here and
/// never reused; later sprints add permissions and mapping rows by a new migration. <c>IdentityLookupSeedTests</c> proves the permission
/// rows are exactly the codes endpoints declare.
/// </summary>
internal static class RoleSeed
{
    public sealed record RoleRow(short Id, string Code, string Name, string Description, bool AllPermissions);

    public sealed record PermissionRow(short Id, string Code, string Name, string Description);

    public sealed record MappingRow(short RoleId, short PermissionId);

    private const short Owner = 1;
    private const short Catalogue = 2;
    private const short OrderOps = 3;
    private const short Support = 4;
    private const short Marketing = 5;

    public static IReadOnlyList<RoleRow> Roles { get; } =
    [
        new(Owner, RoleCodes.Owner, "Owner", "Holds every permission, including ones added later", true),
        new(Catalogue, RoleCodes.Catalogue, "Catalogue", "Maintains styles, products, media and publishing", false),
        new(OrderOps, RoleCodes.OrderOps, "Order operations", "Views stock and the catalogue to dispatch orders; cannot write stock", false),
        new(Support, RoleCodes.Support, "Support", "Customer support; order permissions arrive with the order sprints", false),
        new(Marketing, RoleCodes.Marketing, "Marketing", "Marketing; promotion permissions arrive with the promotions sprint", false),
    ];

    public static IReadOnlyList<PermissionRow> Permissions { get; } =
    [
        new(1, "ref.read", "View reference data", "Read the reference lookups"),
        new(2, "ref.write", "Change reference data", "Change the reference lookups"),
        new(3, "catalog.read", "View the catalogue", "Read styles, products, media, size charts and URLs"),
        new(4, "catalog.write", "Change the catalogue", "Change styles, products, media, composition and publish"),
        new(5, "procurement.read", "View procurement", "Read vendors, purchase orders, revisions and files"),
        new(6, "procurement.write", "Change procurement", "Change vendors, purchase orders, revisions and files"),
        new(7, "production.read", "View production", "Read runs, milestones, samples and deliveries"),
        new(8, "production.write", "Change production", "Change runs, milestones, samples and deliveries"),
        new(9, "qc.read", "View quality control", "Read inbound deliveries, GRNs, batches and inspections"),
        new(10, "qc.write", "Change quality control", "Record inspections and accept or reject batches"),
        new(11, "labelling.read", "View labelling", "Read work items, variants and print jobs"),
        new(12, "labelling.write", "Change labelling", "Assign barcodes and print labels"),
        new(13, "warehouse.read", "View the warehouse", "Read bins and putaway"),
        new(14, "warehouse.write", "Change the warehouse", "Change bins and confirm putaway"),
        new(15, "inventory.read", "View inventory", "Read stock levels, the ledger, adjustments, counts and alerts"),
        new(16, "inventory.write", "Change inventory", "Post adjustments, approve cycle counts and set alert thresholds"),
        new(17, "ops.admin", "Operate the system", "Outbox and operational data, voids"),
        new(18, "staff.manage", "Manage staff", "Staff accounts, roles and MFA reset"),
        new(19, "audit.read", "View the audit trail", "Read the audit trail"),
    ];

    /// <summary>Owner has no rows: the <c>ALL_PERM_IND</c> flag grants everything.</summary>
    public static IReadOnlyList<MappingRow> Mappings { get; } = BuildMappings();

    private static List<MappingRow> BuildMappings()
    {
        var id = Permissions.ToDictionary(p => p.Code, p => p.Id, StringComparer.Ordinal);
        var rows = new List<MappingRow>();

        void Grant(short role, params string[] codes) => rows.AddRange(codes.Select(c => new MappingRow(role, id[c])));

        Grant(Catalogue, "ref.read", "catalog.read", "catalog.write", "warehouse.read");
        Grant(OrderOps, "ref.read", "catalog.read", "warehouse.read", "inventory.read");
        Grant(Support, "ref.read", "catalog.read");
        Grant(Marketing, "ref.read", "catalog.read");

        return rows;
    }
}
