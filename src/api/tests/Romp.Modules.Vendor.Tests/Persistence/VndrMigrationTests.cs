using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests.Persistence;

/// <summary>
/// SCRUM-91: migrating a real PostgreSQL database creates the VNDR vendor tables. Uses
/// Testcontainers (CLAUDE.md); Windows Application Control blocks Docker-backed tests on this
/// machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class VndrMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_CreatesVendorTables()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new VendorDbContext(options);
        await context.Database.MigrateAsync();

        var vendor = new Domain.Vendors.Vendor("Test Vendor", "Contact", "0300", null, cityId: 1, paymentTermId: 1);
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();
        vendor.SetSpecialisations([1, 2]);
        await context.SaveChangesAsync();

        var reloaded = await context.Vendors.Include(v => v.Specialisations).SingleAsync(v => v.Id == vendor.Id);
        Assert.Equal(2, reloaded.Specialisations.Count);
    }
}
