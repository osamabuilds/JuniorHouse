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
        new(20, "orders.view", "View orders", "Read the order queue, order details and COD flags"),
        new(21, "orders.manage", "Manage orders", "Record COD outcomes, cancel orders and clear COD flags"),
        new(22, "discounts.manage", "Manage discount codes", "Create, change and deactivate discount codes"),
        new(23, "shipping.manage", "Manage shipping rates", "Set the shipping fee per city, the default fee and the free-shipping threshold"),
        new(24, "notifications.view", "View notifications", "Read the delivery log and dead letters"),
        new(25, "notifications.resend", "Resend notifications", "Resend a notification from the delivery log"),
        new(26, "fulfilment.view", "View fulfilment", "Read the pick queue, pick lists, the exception queue and packing slips"),
        new(27, "fulfilment.pick", "Pick orders", "Take a pick, scan bins and units, flag a pick exception and complete the pick"),
        new(28, "fulfilment.pack", "Pack orders", "Open an order at the pack station, scan, weigh, complete the pack and print the slip"),
        new(30, "shipping.view", "View shipping", "Read shipments, the manual booking queue, labels and manifests"),
        new(31, "shipping.ops", "Operate shipping", "Retry or hand-enter a booking, print labels, make, count and resolve manifests"),
        new(29, "fulfilment.manage", "Manage fulfilment", "Resolve pick exceptions and take over a pick held by someone else"),
    ];

    /// <summary>Owner has no rows: the <c>ALL_PERM_IND</c> flag grants everything.</summary>
    public static IReadOnlyList<MappingRow> Mappings { get; } = BuildMappings();

    private static List<MappingRow> BuildMappings()
    {
        var id = Permissions.ToDictionary(p => p.Code, p => p.Id, StringComparer.Ordinal);
        var rows = new List<MappingRow>();

        void Grant(short role, params string[] codes) => rows.AddRange(codes.Select(c => new MappingRow(role, id[c])));

        Grant(Catalogue, "ref.read", "catalog.read", "catalog.write", "warehouse.read");
        Grant(OrderOps, "ref.read", "catalog.read", "warehouse.read", "inventory.read", "orders.view", "orders.manage", "notifications.view", "notifications.resend", "fulfilment.view", "fulfilment.pick", "fulfilment.pack", "fulfilment.manage", "shipping.view", "shipping.ops");
        Grant(Support, "ref.read", "catalog.read", "orders.view", "notifications.view", "notifications.resend", "fulfilment.view", "shipping.view");
        Grant(Marketing, "ref.read", "catalog.read", "discounts.manage");

        return rows;
    }
}
