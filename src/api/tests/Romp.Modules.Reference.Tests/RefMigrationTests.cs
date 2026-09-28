using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Domain;
using Romp.Modules.Reference.Infrastructure;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Reference.Tests;

/// <summary>
/// SCRUM-172, AC-1: migrating a real PostgreSQL database creates all eleven REF lookup tables
/// with their seed rows. Uses Testcontainers (CLAUDE.md), not InMemory, because this specifically
/// verifies the migration SQL against the real database engine. Windows Application Control blocks
/// Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class RefMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Migrate_CreatesAllLookupTables_WithSeedRows()
    {
        var options = new DbContextOptionsBuilder<ReferenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new ReferenceDbContext(options);
        await context.Database.MigrateAsync();

        Assert.Equal(10, await context.Set<SizeLookup>().CountAsync());
        Assert.Equal(8, await context.Set<ColourLookup>().CountAsync());
        Assert.Equal(6, await context.Set<FabricLookup>().CountAsync());
        Assert.Equal(3, await context.Set<GenderLookup>().CountAsync());
        Assert.Equal(5, await context.Set<AgeBracketLookup>().CountAsync());
        Assert.Equal(6, await context.Set<CategoryLookup>().CountAsync());
        Assert.Equal(7, await context.Set<CityLookup>().CountAsync());
        Assert.Equal(4, await context.Set<PaymentTermLookup>().CountAsync());
        Assert.Equal(3, await context.Set<VendorSpecialisationLookup>().CountAsync());
        Assert.Equal(4, await context.Set<PoStatusLookup>().CountAsync());
        Assert.Equal(6, await context.Set<PoCancelReasonLookup>().CountAsync());

        var draft = await context.Set<PoStatusLookup>().SingleAsync(s => s.Code == "Draft");
        Assert.True(draft.IsActive);
    }
}
