using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Domain.ProductRecommendations;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.ProductRecommendations;

/// <summary>Recommendations task D1 (REC-2, REC-4, REC-12): the tables against a real PostgreSQL, the seeded rows and the database's own refusals.</summary>
public sealed class RecommendationMigrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private async Task<StfrDbContext> MigratedAsync()
    {
        var context = new StfrDbContext(new DbContextOptionsBuilder<StfrDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
            .Options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static T Stamp<T>(T entity)
        where T : IAuditable
    {
        entity.InsrDte = Now;
        entity.InsrBy = "test";
        return entity;
    }

    private static PublishedProduct Product(long id) =>
        Stamp(new PublishedProduct(
            id, 1, $"/girls/dresses/floral-{id}", "Floral dress", "A dress.", "STY-1",
            "GIRLS", "Girls", "DRESSES", "Dresses", "KIDS", "Kids 4-8Y", Now, null));

    private static async Task SeedProductsAsync(StfrDbContext context)
    {
        context.Products.AddRange(Product(1), Product(2));
        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task Migrate_SeedsTheTwoRowCodes_WithTheEnumIds()
    {
        await using var context = await MigratedAsync();

        var rows = await context.Set<RecommendationRowLookup>().OrderBy(l => l.Id).ToListAsync();

        Assert.Equal([RecommendationRowId.Similar, RecommendationRowId.Trending], rows.Select(r => r.Id));
        Assert.Equal(["SIMILAR", "TRENDING"], rows.Select(r => r.Code));
    }

    [Fact]
    [Trait("Spec", "REC-2")]
    public async Task Database_RefusesADuplicateRank_ADuplicatePair_RankThirteenAndASelfPair()
    {
        await using var context = await MigratedAsync();
        await SeedProductsAsync(context);
        context.SimilarProducts.Add(Stamp(new SimilarProduct(1, 2, 1, 0.5, Now)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            context.SimilarProducts.Add(Stamp(new SimilarProduct(1, 2, 2, 0.4, Now)));
            await context.SaveChangesAsync();
        });
        context.ChangeTracker.Clear();

        // Same rank, other product: caught by the unique (product, rank), not the pair.
        context.Products.Add(Product(3));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            context.SimilarProducts.Add(Stamp(new SimilarProduct(1, 3, 1, 0.4, Now)));
            await context.SaveChangesAsync();
        });
        context.ChangeTracker.Clear();

        // The entity refuses rank 13 and a self pair first; the table's own checks are proved with raw SQL.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"STFR\".\"PRDT_SMLR\" (\"PRDT_ID\",\"SMLR_PRDT_ID\",\"RANK_NO\",\"SCOR_VAL\",\"CALC_DTE\",\"INSR_DTE\",\"INSR_BY\") VALUES (1,3,13,0.1,now(),now(),'t')"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"STFR\".\"PRDT_SMLR\" (\"PRDT_ID\",\"SMLR_PRDT_ID\",\"RANK_NO\",\"SCOR_VAL\",\"CALC_DTE\",\"INSR_DTE\",\"INSR_BY\") VALUES (1,1,5,0.1,now(),now(),'t')"));
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Database_RefusesADuplicateProductDay_AndANegativeCount()
    {
        await using var context = await MigratedAsync();
        await SeedProductsAsync(context);
        context.ProductSaleDays.Add(Stamp(new ProductSaleDay(1, new DateOnly(2026, 11, 1), 3)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            context.ProductSaleDays.Add(Stamp(new ProductSaleDay(1, new DateOnly(2026, 11, 1), 1)));
            await context.SaveChangesAsync();
        });
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"STFR\".\"PRDT_SALE_DAY\" (\"PRDT_ID\",\"SALE_DTE\",\"UNIT_QTY\",\"INSR_DTE\",\"INSR_BY\") VALUES (2,'2026-11-01',-1,now(),'t')"));
    }

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task Database_RefusesAnUnknownRowCode_AndADuplicateEventDay()
    {
        await using var context = await MigratedAsync();
        await SeedProductsAsync(context);
        context.RecommendationDays.Add(Stamp(new RecommendationDay(RecommendationRowId.Similar, 1, new DateOnly(2026, 11, 8))));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            context.RecommendationDays.Add(Stamp(new RecommendationDay(RecommendationRowId.Similar, 1, new DateOnly(2026, 11, 8))));
            await context.SaveChangesAsync();
        });
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            context.RecommendationDays.Add(Stamp(new RecommendationDay((RecommendationRowId)9, 1, new DateOnly(2026, 11, 8))));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public async Task UnpublishingAProduct_RemovesItsSimilarRowsAndSaleDays()
    {
        await using var context = await MigratedAsync();
        await SeedProductsAsync(context);
        context.SimilarProducts.Add(Stamp(new SimilarProduct(1, 2, 1, 0.5, Now)));
        context.SimilarProducts.Add(Stamp(new SimilarProduct(2, 1, 1, 0.5, Now)));
        context.ProductSaleDays.Add(Stamp(new ProductSaleDay(2, new DateOnly(2026, 11, 1), 3)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await context.Products.Where(p => p.Id == 2).ExecuteDeleteAsync();

        Assert.Equal(0, await context.SimilarProducts.CountAsync());
        Assert.Equal(0, await context.ProductSaleDays.CountAsync());
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public async Task Migrate_LeavesNoPendingModelChanges()
    {
        await using var context = await MigratedAsync();

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
