using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.Persistence;

/// <summary>
/// Published-catalog task 9: the read-model tables against a real PostgreSQL (Testcontainers, CLAUDE.md) - lookups seeded with the enum
/// ids, the keys and uniques the projection and listings rely on, the database's own refusals, and the cascade from a product to its rows.
/// </summary>
public sealed class ReadModelMigrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

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

    private static PublishedProduct Product(long id, string? path = null) =>
        Stamp(new PublishedProduct(
            id, 1, path ?? $"/girls/dresses/floral-{id}", "Floral dress", "A dress.", "STY-1",
            "GIRLS", "Girls", "DRESSES", "Dresses", "KIDS", "Kids 4-8Y", Now, "Wash separately"));

    private static VariantRow Variant(long id, long productId, string? sku, decimal? price = 1299.50m, decimal? compareAt = null) =>
        Stamp(new VariantRow(id, productId, 1, "RED", "Red", 4, "5-6Y", "5-6 Years", 5, sku, price, compareAt, sku is not null));

    private static async Task<DbUpdateException> RefusedAsync(StfrDbContext context) =>
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Migrate_Lookups_AreSeededWithTheEnumIds_AndTheFormatCodesMatchTheDomain()
    {
        await using var context = await MigratedAsync();

        Assert.Equal(["PENDING", "DONE", "FAILED"], await context.Set<DerivativeStatusLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["RUNNING", "COMPLETED", "FAILED"], await context.Set<RebuildStatusLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        var formats = await context.Set<DerivativeFormatLookup>().OrderBy(l => l.Id).ToListAsync();
        Assert.Equal([DerivativeFormat.Webp, DerivativeFormat.Jpeg], formats.Select(f => f.Id));
        Assert.All(formats, f => Assert.Equal(f.Id.Code(), f.Code));
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Migrate_CreatesTheReadModelTables_AndTheInbox_AndNoPendingModelChanges()
    {
        await using var context = await MigratedAsync();
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        var tables = await ScalarListAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'STFR'");

        foreach (var expected in new[]
        {
            "PRDT_MAIN", "PRDT_VRNT", "PRDT_IMG", "IMG_DRV", "PRDT_CMPS", "PRDT_CARE", "PRDT_COLN_MAP",
            "CHRT_MAIN", "CHRT_LINE", "AVLB_STK", "RBLD_RUN", "MODL_META", "INBX",
            "DRV_STS_LKP", "DRV_FMT_LKP", "RBLD_STS_LKP",
        })
        {
            Assert.Contains(expected, tables);
        }

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task TheModelMetaRow_IsSeededAtVersionZero_AndIsTheOnlyRowTheDatabaseAllows()
    {
        await using var context = await MigratedAsync();

        var meta = await context.ModelMeta.AsNoTracking().SingleAsync();
        Assert.Equal((ModelMeta.OnlyId, 0), (meta.Id, meta.ModelVersion));

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO \"STFR\".\"MODL_META\" (\"ID\", \"MODL_VER\", \"INSR_DTE\", \"INSR_BY\") VALUES (2, 1, now(), 'x')", connection);
            await insert.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task AProductWithEverythingAPageShows_RoundTripsWithItsExactValues()
    {
        await using (var write = await MigratedAsync())
        {
            var product = Product(7);
            write.Products.Add(product);
            await write.SaveChangesAsync();
            write.Variants.Add(Variant(70, 7, "ROMP-S1-RED-5", 1299.50m, 1799.99m));
            write.Images.Add(Stamp(new ImageRow(900, 7, 1, "images/abc.png", 640, 960, "A red floral dress, front view", 1, true)));
            write.Composition.Add(Stamp(new CompositionRow(7, "COTTON", "Cotton", 95.5m, 1)));
            write.CareInstructions.Add(Stamp(new CareRow(7, "WASH-30", "Machine wash at 30 C", "wash-30", 1)));
            write.Collections.Add(Stamp(new CollectionRow(7, "SALE", "Sale")));
            await write.SaveChangesAsync();
            write.Derivatives.Add(Stamp(new DerivativeRow(900, 640, DerivativeFormat.Webp)));
            await write.SaveChangesAsync();
        }

        await using var read = await MigratedAsync();
        var variant = await read.Variants.AsNoTracking().SingleAsync(v => v.ProductId == 7);
        Assert.Equal((1299.50m, 1799.99m, "romp-s1-red-5", 0L, (int?)null), (variant.Price, variant.CompareAtPrice, variant.SkuNormalised, variant.StockVersion, variant.AvailableQuantity));
        Assert.Equal(95.5m, (await read.Composition.AsNoTracking().SingleAsync(c => c.ProductId == 7)).Percent);
        var derivative = await read.Derivatives.AsNoTracking().SingleAsync(d => d.ImageId == 900);
        Assert.Equal((DerivativeStatus.Pending, 0, DerivativeFormat.Webp), (derivative.Status, derivative.AttemptCount, derivative.Format));
        Assert.Equal("Floral dress", (await read.Products.AsNoTracking().SingleAsync(p => p.Id == 7)).Name);
    }

    [Fact]
    [Trait("Spec", "PCT-2")]
    public async Task DeletingAProduct_RemovesEveryRowThatBelongsToIt()
    {
        await using var context = await MigratedAsync();
        context.Products.Add(Product(8));
        await context.SaveChangesAsync();
        context.Variants.Add(Variant(80, 8, "ROMP-S2-RED-5"));
        context.Images.Add(Stamp(new ImageRow(800, 8, null, "images/x.png", 10, 10, "alt", 1, true)));
        context.Composition.Add(Stamp(new CompositionRow(8, "COTTON", "Cotton", 100m, 1)));
        context.CareInstructions.Add(Stamp(new CareRow(8, "WASH-30", "Wash", "wash-30", 1)));
        context.Collections.Add(Stamp(new CollectionRow(8, "SALE", "Sale")));
        await context.SaveChangesAsync();
        context.Derivatives.Add(Stamp(new DerivativeRow(800, 320, DerivativeFormat.Jpeg)));
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"STFR\".\"PRDT_MAIN\" WHERE \"ID\" = 8");

        Assert.False(await context.Variants.AnyAsync(v => v.ProductId == 8));
        Assert.False(await context.Images.AnyAsync(i => i.ProductId == 8));
        Assert.False(await context.Derivatives.AnyAsync(d => d.ImageId == 800));
        Assert.False(await context.Composition.AnyAsync(c => c.ProductId == 8));
        Assert.False(await context.CareInstructions.AnyAsync(c => c.ProductId == 8));
        Assert.False(await context.Collections.AnyAsync(c => c.ProductId == 8));
    }

    [Fact]
    [Trait("Spec", "ST-16")]
    public async Task TheUniques_ThatTheProjectionAndTheListingsRelyOn_AreEnforcedByTheDatabase()
    {
        await using var context = await MigratedAsync();
        context.Products.Add(Product(10, "/girls/dresses/one"));
        await context.SaveChangesAsync();

        context.Products.Add(Product(11, "/girls/dresses/one"));
        Assert.Contains("IX_PRDT_MAIN_PATH", (await RefusedAsync(context)).InnerException?.Message);
        context.ChangeTracker.Clear();

        context.Products.Add(Product(12, "/girls/dresses/two"));
        context.Variants.AddRange(Variant(101, 12, null), Variant(102, 12, null)); // many variants may have no SKU
        await context.SaveChangesAsync();
        context.Variants.Add(Variant(103, 12, "ROMP-DUP"));
        await context.SaveChangesAsync();
        context.Variants.Add(Variant(104, 12, "  romp-dup "));
        Assert.Contains("IX_PRDT_VRNT_SKU_NORM", (await RefusedAsync(context)).InnerException?.Message);
        context.ChangeTracker.Clear();

        context.Stock.Add(Stamp(new StockRow(1, "ROMP-DUP", 3, 1)));
        await context.SaveChangesAsync();
        context.Stock.Add(Stamp(new StockRow(2, "ROMP-DUP", 3, 1)));
        Assert.Contains("IX_AVLB_STK_SKU_CODE", (await RefusedAsync(context)).InnerException?.Message);
        context.ChangeTracker.Clear();

        context.Charts.Add(Stamp(new SizeChartRow(1, 1, "DRESSES", "Dresses", "GIRLS", "Girls", "Dresses")));
        await context.SaveChangesAsync();
        context.Charts.Add(Stamp(new SizeChartRow(2, 2, "DRESSES", "Dresses", "GIRLS", "Girls", "Again")));
        Assert.Contains("IX_CHRT_MAIN_CATG_CODE_GNDR_CODE", (await RefusedAsync(context)).InnerException?.Message);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task OneImageHasAtMostOneDerivative_PerWidthAndFormat()
    {
        await using var context = await MigratedAsync();
        context.Products.Add(Product(20));
        await context.SaveChangesAsync();
        context.Images.Add(Stamp(new ImageRow(2000, 20, null, "images/y.png", 10, 10, "alt", 1, true)));
        await context.SaveChangesAsync();
        context.Derivatives.Add(Stamp(new DerivativeRow(2000, 640, DerivativeFormat.Webp)));
        await context.SaveChangesAsync();

        context.Derivatives.Add(Stamp(new DerivativeRow(2000, 640, DerivativeFormat.Jpeg)));
        await context.SaveChangesAsync(); // another format is a different file
        context.Derivatives.Add(Stamp(new DerivativeRow(2000, 640, DerivativeFormat.Webp)));

        Assert.Contains("IX_IMG_DRV_IMG_ID_WDTH_DRV_FMT_ID", (await RefusedAsync(context)).InnerException?.Message);
    }

    [Theory]
    [Trait("Spec", "SEC-09")]
    [InlineData("PRDT_VRNT", "\"PRIC\" = 0", "CK_PRDT_VRNT_PRIC_POS")]
    [InlineData("PRDT_VRNT", "\"PRIC\" = 100, \"CMPR_PRIC\" = 100", "CK_PRDT_VRNT_CMPR_PRIC")]
    [InlineData("PRDT_VRNT", "\"AVLB_QTY\" = -1", "CK_PRDT_VRNT_AVLB_QTY")]
    [InlineData("PRDT_MAIN", "\"MIN_PRIC\" = 500, \"MAX_PRIC\" = 400", "CK_PRDT_MAIN_PRIC_ORDR")]
    [InlineData("PRDT_MAIN", "\"MAX_DISC_PCT\" = 100", "CK_PRDT_MAIN_DISC_PCT")]
    [InlineData("AVLB_STK", "\"QTY\" = -1", "CK_AVLB_STK_QTY_NONNEG")]
    [InlineData("PRDT_CMPS", "\"PCT\" = 0", "CK_PRDT_CMPS_PCT_RNG")]
    public async Task ANonsenseValue_IsRefusedByTheDatabase_WhateverWroteIt(string table, string assignment, string constraint)
    {
        await using var context = await MigratedAsync();
        context.Products.Add(Product(30));
        context.Stock.Add(Stamp(new StockRow(30, "ROMP-30", 1, 1)));
        await context.SaveChangesAsync();
        context.Variants.Add(Variant(300, 30, "ROMP-30-A", 100m));
        context.Composition.Add(Stamp(new CompositionRow(30, "COTTON", "Cotton", 100m, 1)));
        await context.SaveChangesAsync();

        var update = "UPDATE \"STFR\".\"" + table + "\" SET " + assignment; // fixed constants from the InlineData above, not input
        var ex = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(update));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal(constraint, ex.ConstraintName);
    }

    private static async Task<List<string>> ScalarListAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
