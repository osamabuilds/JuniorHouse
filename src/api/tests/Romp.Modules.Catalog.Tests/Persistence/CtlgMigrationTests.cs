using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Catalog.Tests.Persistence;

/// <summary>
/// SCRUM-173: migrating a real PostgreSQL database creates the CTLG style tables. Uses
/// Testcontainers (CLAUDE.md); Windows Application Control blocks Docker-backed tests on this
/// machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class CtlgMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_CreatesStyleTables()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new CatalogDbContext(options);
        await context.Database.MigrateAsync();

        var style = new Style("STY-001", "Test Style", null, 1, 1, 1, 1, 100m, 300m);
        context.Styles.Add(style);
        await context.SaveChangesAsync();

        style.SetColourSizeAndTargets([1, 2], [1], [(1, 1, 10), (1, 2, 20)]);
        await context.SaveChangesAsync();

        var reloaded = await context.Styles
            .Include(s => s.Colourways)
            .Include(s => s.Sizes)
            .Include(s => s.TargetLines)
            .SingleAsync(s => s.Id == style.Id);

        Assert.Equal(2, reloaded.Colourways.Count);
        Assert.Single(reloaded.Sizes);
        Assert.Equal(2, reloaded.TargetLines.Count);
    }
}
