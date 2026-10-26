using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Domain.Lookups.Vendors;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Reference.Tests.Persistence;

/// <summary>customers/addresses A1 (ADD-4): provinces are seeded and every seeded city belongs to one. Real PostgreSQL via Testcontainers.</summary>
public sealed class GeographyMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private async Task<ReferenceDbContext> MigratedAsync()
    {
        var context = new ReferenceDbContext(new DbContextOptionsBuilder<ReferenceDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        await context.Database.MigrateAsync();
        return context;
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Migrate_SeedsTheSevenProvincesAndTerritories()
    {
        await using var context = await MigratedAsync();

        var codes = await context.Set<ProvinceLookup>().OrderBy(p => p.SortSeq).Select(p => p.Code).ToListAsync();

        Assert.Equal(["PB", "SD", "KP", "BL", "ICT", "GB", "AJK"], codes);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Migrate_MapsEveryExistingAndSeededCityToItsProvince()
    {
        await using var context = await MigratedAsync();

        var cities = await context.Set<CityLookup>().ToListAsync();
        var provinceByCode = await context.Set<ProvinceLookup>().ToDictionaryAsync(p => p.Id, p => p.Code);

        Assert.True(cities.Count >= 20);
        Assert.All(cities, c => Assert.NotNull(c.ProvinceId));
        Assert.Equal("ICT", provinceByCode[cities.Single(c => c.Code == "ISB").ProvinceId!.Value]);
        Assert.Equal("SD", provinceByCode[cities.Single(c => c.Code == "KHI").ProvinceId!.Value]);
        Assert.Equal("PB", provinceByCode[cities.Single(c => c.Code == "LHE").ProvinceId!.Value]);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Migrate_DeclaresAndIndexesTheCityProvinceForeignKey()
    {
        await using var context = await MigratedAsync();

        var fk = await context.Database.SqlQueryRaw<string>(
            "SELECT conname AS \"Value\" FROM pg_constraint WHERE conrelid = '\"REF\".\"CITY_LKP\"'::regclass AND contype = 'f'").ToListAsync();
        var ix = await context.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'REF' AND tablename = 'CITY_LKP' AND indexdef LIKE '%(\"PROV_ID\")%'").ToListAsync();

        Assert.Single(fk);
        Assert.Single(ix);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Migrate_LeavesTheIdSequencesReadyForRowsStaffAddLater()
    {
        await using var context = await MigratedAsync();

        context.Set<ProvinceLookup>().Add(new ProvinceLookup("NEW-P", "New province", null, 99));
        context.Set<CityLookup>().Add(new CityLookup("NEW-C", "New city", null, 99));
        await context.SaveChangesAsync();

        Assert.True(await context.Set<CityLookup>().AnyAsync(c => c.Code == "NEW-C" && c.ProvinceId == null));
    }
}
