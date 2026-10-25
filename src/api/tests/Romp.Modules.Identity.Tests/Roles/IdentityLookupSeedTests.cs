using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Identity.Tests.Roles;

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the IDNT migrations applied, shared by the tests of this class.</summary>
public sealed class IdentityPostgres : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    internal IdntDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<IdntDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "IDNT"))
            .Options;
        return new IdntDbContext(options);
    }
}

/// <summary>Roles spec AC-1, AC-2, AC-8, AC-14, AC-15: the migration seeds the permission lookup, the five roles and the mapping.</summary>
public sealed class IdentityLookupSeedTests(IdentityPostgres postgres) : IClassFixture<IdentityPostgres>
{
    [Fact]
    [Trait("Spec", "FR-60")]
    [Trait("Spec", "SEC-08")]
    public async Task PermissionLookup_HoldsExactlyTheCodesEndpointsDeclare()
    {
        await using var context = postgres.NewContext();

        var seeded = await context.Permissions.AsNoTracking().Select(p => p.Code).ToListAsync();

        Assert.Equal(
            Permissions.All.Select(p => p.Value).Order(StringComparer.Ordinal),
            seeded.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "FR-60")]
    public async Task Roles_AreTheFiveFromTheSpec()
    {
        await using var context = postgres.NewContext();

        var codes = await context.Roles.AsNoTracking().Select(r => r.Code).ToListAsync();

        Assert.Equal(["CATALOGUE", "MARKETING", "ORDER_OPS", "OWNER", "SUPPORT"], codes.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "FR-60")]
    public async Task Owner_HoldsEveryPermissionByFlag_AndNoOtherRoleDoes()
    {
        await using var context = postgres.NewContext();

        var flagged = await context.Roles.AsNoTracking().Where(r => r.HasAllPermissions).Select(r => r.Code).ToListAsync();
        var ownerMappings = await context.RolePermissions.AsNoTracking().CountAsync(m => m.Role!.Code == "OWNER");

        Assert.Equal(["OWNER"], flagged);
        Assert.Equal(0, ownerMappings); // a superset flag, not a copied list, so later permissions need no mapping row (AC-2)
    }

    [Fact]
    [Trait("Spec", "FR-60")]
    public async Task OrderOps_HoldsInventoryRead_AndNoWritePermission()
    {
        var codes = await PermissionsOfAsync("ORDER_OPS");

        Assert.Contains("inventory.read", codes);
        Assert.DoesNotContain(codes, c => c.EndsWith(".write", StringComparison.Ordinal));
        Assert.DoesNotContain("ops.admin", codes);
    }

    [Theory]
    [InlineData("CATALOGUE", new[] { "ref.read", "catalog.read", "catalog.write", "warehouse.read" })]
    [InlineData("ORDER_OPS", new[] { "ref.read", "catalog.read", "warehouse.read", "inventory.read" })]
    [InlineData("SUPPORT", new[] { "ref.read", "catalog.read" })]
    [InlineData("MARKETING", new[] { "ref.read", "catalog.read" })]
    [Trait("Spec", "FR-60")]
    public async Task Role_HoldsExactlyThePermissionsOfTheSpec(string role, string[] expected)
    {
        var codes = await PermissionsOfAsync(role);

        Assert.Equal(expected.Order(StringComparer.Ordinal), codes.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "FR-60")]
    public async Task SupportAndMarketing_HoldNoWritePermission()
    {
        foreach (var role in new[] { "SUPPORT", "MARKETING" })
        {
            Assert.DoesNotContain(await PermissionsOfAsync(role), c => c.EndsWith(".write", StringComparison.Ordinal));
        }
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task EveryRoleHoldingAWritePermission_AlsoHoldsItsReadPermission()
    {
        await using var context = postgres.NewContext();
        var roles = await context.Roles.AsNoTracking().Where(r => !r.HasAllPermissions).Select(r => r.Code).ToListAsync();
        var codesPermissions = Permissions.All.Select(p => p.Value).ToHashSet(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            var held = (await PermissionsOfAsync(role)).ToHashSet(StringComparer.Ordinal);

            foreach (var write in held.Where(c => c.EndsWith(".write", StringComparison.Ordinal)))
            {
                var read = write[..^".write".Length] + ".read";

                // A group is declared with its read permission and a write route adds the write one, so write without read would 403 (roles plan).
                Assert.True(!codesPermissions.Contains(read) || held.Contains(read), $"{role} holds {write} but not {read}.");
            }
        }
    }

    private async Task<List<string>> PermissionsOfAsync(string roleCode)
    {
        await using var context = postgres.NewContext();
        return await context.RolePermissions.AsNoTracking()
            .Where(m => m.Role!.Code == roleCode)
            .Select(m => m.Permission!.Code)
            .ToListAsync();
    }
}
