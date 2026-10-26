using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Roles;

/// <summary>Roles task R1 (AC-2, AC-3): what a person may do is worked out from the database, in one query, whenever it is asked.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class EffectivePermissionsTests(IdentityPostgres postgres)
{
    private static readonly string[] OrderOpsPermissions = ["ref.read", "catalog.read", "warehouse.read", "inventory.read"];
    private static readonly string[] CataloguePermissions = ["ref.read", "catalog.read", "catalog.write", "warehouse.read"];

    private static async Task<long> UserWithAsync(IdentityHost host, params string[] roleCodes)
    {
        var ids = new List<short>();
        foreach (var code in roleCodes)
        {
            ids.Add(await host.RoleIdAsync(code));
        }

        return (await host.SendAsync(new CreateStaffCommand(IdentityHost.NewUserName(), "Role Person", ids))).UserId;
    }

    private static async Task<IReadOnlySet<string>> PermissionsAsync(IdentityHost host, long userId) =>
        await host.SendAsync(new GetEffectivePermissionsQuery(userId));

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Owner_HasEverythingIncludingFutureRows()
    {
        using var host = new IdentityHost(postgres);
        var owner = await UserWithAsync(host, RoleCodes.Owner);

        var now = await PermissionsAsync(host, owner);
        Assert.Equal(Permissions.All.Select(p => p.Value).Order(StringComparer.Ordinal), now.Order(StringComparer.Ordinal));

        // A permission added by a later migration is granted to the Owner with no mapping row at all.
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"IDNT\".\"PERM_LKP\" (\"ID\", \"CODE\", \"NAME\", \"SORT_SEQ\", \"ACT_IND\") VALUES (9001, 'future.write', 'Future', 900, true)");
        try
        {
            var later = await PermissionsAsync(host, owner);
            Assert.Contains("future.write", later);

            var catalogue = await PermissionsAsync(host, await UserWithAsync(host, RoleCodes.Catalogue));
            Assert.DoesNotContain("future.write", catalogue);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IDNT\".\"PERM_LKP\" WHERE \"ID\" = 9001");
        }
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task SeveralRoles_AreUnioned()
    {
        using var host = new IdentityHost(postgres);
        var both = await UserWithAsync(host, RoleCodes.Catalogue, RoleCodes.OrderOps);
        var catalogueOnly = await UserWithAsync(host, RoleCodes.Catalogue);
        var orderOnly = await UserWithAsync(host, RoleCodes.OrderOps);

        Assert.Equal(CataloguePermissions.Union(OrderOpsPermissions).Order(StringComparer.Ordinal), (await PermissionsAsync(host, both)).Order(StringComparer.Ordinal));
        Assert.Equal(CataloguePermissions.Order(StringComparer.Ordinal), (await PermissionsAsync(host, catalogueOnly)).Order(StringComparer.Ordinal));
        Assert.Equal(OrderOpsPermissions.Order(StringComparer.Ordinal), (await PermissionsAsync(host, orderOnly)).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task SupportAndMarketing_HoldNoWritePermission()
    {
        using var host = new IdentityHost(postgres);

        foreach (var role in new[] { RoleCodes.Support, RoleCodes.Marketing })
        {
            var permissions = await PermissionsAsync(host, await UserWithAsync(host, role));
            Assert.Equal(["catalog.read", "ref.read"], permissions.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task AnInactiveAccount_AccountWithNoRoles_AndAnUnknownId_HaveNothing()
    {
        using var host = new IdentityHost(postgres);
        var inactive = await UserWithAsync(host, RoleCodes.Owner);
        var spare = await UserWithAsync(host, RoleCodes.Owner); // so the last Owner rule does not block the deactivation
        await host.SendAsync(new DeactivateStaffCommand(inactive));
        var noRoles = (await host.SendAsync(new CreateStaffCommand(IdentityHost.NewUserName(), "No Roles", []))).UserId;

        Assert.Empty(await PermissionsAsync(host, inactive));
        Assert.Empty(await PermissionsAsync(host, noRoles));
        Assert.Empty(await PermissionsAsync(host, long.MaxValue));
        Assert.NotEmpty(await PermissionsAsync(host, spare));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task InactiveRolesAndPermissions_AreIgnored_AndAChangeAppliesAtOnce()
    {
        using var host = new IdentityHost(postgres);
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        try
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"IDNT\".\"PERM_LKP\" SET \"ACT_IND\" = false WHERE \"CODE\" = 'catalog.write'");
            Assert.DoesNotContain("catalog.write", await PermissionsAsync(host, user));
            Assert.Contains("catalog.read", await PermissionsAsync(host, user));

            await db.Database.ExecuteSqlRawAsync("UPDATE \"IDNT\".\"ROLE_LKP\" SET \"ACT_IND\" = false WHERE \"CODE\" = 'CATALOGUE'");
            Assert.Empty(await PermissionsAsync(host, user)); // nothing is remembered between calls
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"IDNT\".\"PERM_LKP\" SET \"ACT_IND\" = true WHERE \"CODE\" = 'catalog.write'");
            await db.Database.ExecuteSqlRawAsync("UPDATE \"IDNT\".\"ROLE_LKP\" SET \"ACT_IND\" = true WHERE \"CODE\" = 'CATALOGUE'");
        }

        Assert.Contains("catalog.write", await PermissionsAsync(host, user));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task TheAnswer_IsOneQuery()
    {
        var counter = new CommandCounter();
        using var host = new IdentityHost(postgres, counter);
        var user = await UserWithAsync(host, RoleCodes.Catalogue, RoleCodes.OrderOps);
        counter.Reset();

        await PermissionsAsync(host, user);

        Assert.Equal(1, counter.Count);
    }
}
