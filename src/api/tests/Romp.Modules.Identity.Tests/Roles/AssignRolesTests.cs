using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Roles;

/// <summary>Roles task R3 (AC-6): an administrator replaces a person's roles; the last active Owner cannot lose the Owner role.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class AssignRolesTests(IdentityPostgres postgres)
{
    private static async Task<short[]> IdsAsync(IdentityHost host, params string[] codes)
    {
        var ids = new List<short>();
        foreach (var code in codes)
        {
            ids.Add(await host.RoleIdAsync(code));
        }

        return [.. ids];
    }

    private static async Task<long> UserWithAsync(IdentityHost host, params string[] codes) =>
        (await host.SendAsync(new CreateStaffCommand(IdentityHost.NewUserName(), "Role Person", await IdsAsync(host, codes)))).UserId;

    private static async Task<List<short>> RoleIdsOfAsync(IdentityHost host, long userId)
    {
        await using var scope = host.Scope();
        return await scope.ServiceProvider.GetRequiredService<IdntDbContext>().Set<StaffUserRole>().AsNoTracking()
            .Where(r => r.UserId == userId).Select(r => r.RoleId).OrderBy(id => id).ToListAsync();
    }

    private static async Task MakeOtherOwnersInactiveAsync(IdentityHost host, long except)
    {
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        var owner = await host.RoleIdAsync(RoleCodes.Owner);
        await db.Staff.Where(u => u.Id != except && u.Roles.Any(r => r.RoleId == owner)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task ReplacesTheSet_AndItTakesEffectAtOnce()
    {
        using var host = new IdentityHost(postgres);
        var user = await UserWithAsync(host, RoleCodes.Catalogue);

        await host.SendAsync(new AssignRolesCommand(user, await IdsAsync(host, RoleCodes.OrderOps, RoleCodes.Support)));

        var expected = (await IdsAsync(host, RoleCodes.OrderOps, RoleCodes.Support)).Order().ToList();
        Assert.Equal(expected, await RoleIdsOfAsync(host, user));
        var permissions = await host.SendAsync(new GetEffectivePermissionsQuery(user));
        Assert.Contains("inventory.read", permissions);
        Assert.DoesNotContain("catalog.write", permissions); // the Catalogue role is gone

        // Taking every role away leaves an account that can sign in and see nothing.
        await host.SendAsync(new AssignRolesCommand(user, []));
        Assert.Empty(await RoleIdsOfAsync(host, user));
        Assert.Empty(await host.SendAsync(new GetEffectivePermissionsQuery(user)));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task RepeatingTheSameSet_ChangesNothing_AndDuplicatesCollapse()
    {
        using var host = new IdentityHost(postgres);
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        var catalogue = await host.RoleIdAsync(RoleCodes.Catalogue);

        await host.SendAsync(new AssignRolesCommand(user, [catalogue, catalogue]));
        await host.SendAsync(new AssignRolesCommand(user, [catalogue]));

        Assert.Equal([catalogue], await RoleIdsOfAsync(host, user));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task LastOwner_CannotLoseTheRole()
    {
        using var host = new IdentityHost(postgres);
        var owner = await UserWithAsync(host, RoleCodes.Owner);
        await MakeOtherOwnersInactiveAsync(host, except: owner);
        var catalogue = await host.RoleIdAsync(RoleCodes.Catalogue);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new AssignRolesCommand(owner, [catalogue])));
        Assert.Contains(await host.RoleIdAsync(RoleCodes.Owner), await RoleIdsOfAsync(host, owner));

        // With another active Owner, the role can move.
        var other = await UserWithAsync(host, RoleCodes.Owner);
        await host.SendAsync(new AssignRolesCommand(owner, [catalogue]));
        Assert.Equal([catalogue], await RoleIdsOfAsync(host, owner));
        Assert.Contains(await host.RoleIdAsync(RoleCodes.Owner), await RoleIdsOfAsync(host, other));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task AnOwner_CanBeGivenMoreRoles_WithoutTheGuardInterfering()
    {
        using var host = new IdentityHost(postgres);
        var owner = await UserWithAsync(host, RoleCodes.Owner);
        await MakeOtherOwnersInactiveAsync(host, except: owner);

        await host.SendAsync(new AssignRolesCommand(owner, await IdsAsync(host, RoleCodes.Owner, RoleCodes.Catalogue)));

        Assert.Equal(2, (await RoleIdsOfAsync(host, owner)).Count);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task AtMostFiveRoles_AndOnlyRolesThatExist()
    {
        using var host = new IdentityHost(postgres);
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        var all = await IdsAsync(host, RoleCodes.Owner, RoleCodes.Catalogue, RoleCodes.OrderOps, RoleCodes.Support, RoleCodes.Marketing);

        await host.SendAsync(new AssignRolesCommand(user, all.Where(id => id != all[0]).ToArray())); // four of them: fine
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new AssignRolesCommand(user, [.. all, 77, 78]))); // too many
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new AssignRolesCommand(user, [short.MaxValue]))); // no such role
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new AssignRolesCommand(user, [0])));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new AssignRolesCommand(0, [])));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new AssignRolesCommand(long.MaxValue, [])));

        Assert.Equal(4, (await RoleIdsOfAsync(host, user)).Count); // the refused requests changed nothing
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task AnInactiveRole_CannotBeGiven()
    {
        using var host = new IdentityHost(postgres);
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        try
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"IDNT\".\"ROLE_LKP\" SET \"ACT_IND\" = false WHERE \"CODE\" = 'MARKETING'");

            var marketing = await host.RoleIdAsync(RoleCodes.Marketing);
            await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new AssignRolesCommand(user, [marketing])));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"IDNT\".\"ROLE_LKP\" SET \"ACT_IND\" = true WHERE \"CODE\" = 'MARKETING'");
        }
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class RoleMatrixQueryTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task TwoQueries_NoNPlusOne()
    {
        var counter = new CommandCounter();
        using var host = new IdentityHost(postgres, counter);
        counter.Reset();

        var matrix = await host.SendAsync(new GetRoleMatrixQuery());

        Assert.Equal(2, counter.Count);
        Assert.Equal(Permissions.All.Count, matrix.Permissions.Count);
        var owner = Assert.Single(matrix.Roles, r => r.Code == RoleCodes.Owner);
        Assert.True(owner.HasAllPermissions);
        Assert.Equal(Permissions.All.Count, owner.PermissionCodes.Count); // the superset flag, shown as every permission
        var orderOps = Assert.Single(matrix.Roles, r => r.Code == RoleCodes.OrderOps);
        Assert.Equal(["catalog.read", "inventory.read", "ref.read", "warehouse.read"], orderOps.PermissionCodes.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(orderOps.PermissionCodes, c => c.EndsWith(".write", StringComparison.Ordinal));
    }
}
