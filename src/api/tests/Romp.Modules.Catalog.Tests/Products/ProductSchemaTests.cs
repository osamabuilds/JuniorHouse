using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Domain.CompositionCare;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Catalog.Domain.Urls;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Products;

/// <summary>
/// The database has the last word (PRD-7, PRD-8, MED-5, URL-6, URL-11, CHT-4, CHT-5, CMP-6): every rule below is refused by PostgreSQL
/// itself when something writes around the application, on a real database (Testcontainers).
/// </summary>
[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class ProductSchemaTests(CatalogPostgres postgres)
{
    private static async Task<PostgresException> RefusedAsync(Func<Task> action) =>
        (await Assert.ThrowsAnyAsync<Exception>(action)) switch
        {
            DbUpdateException { InnerException: PostgresException pg } => pg,
            PostgresException pg => pg,
            var other => throw new Xunit.Sdk.XunitException($"Expected the database to refuse the write, got {other.GetType().Name}: {other.Message}"),
        };

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task ASecondProductForTheSameStyle_IsRefusedByTheDatabase()
    {
        using var host = new CatalogHost(postgres);
        var styleId = await host.NewStyleAsync();
        await host.NewProductAsync(styleId);
        await using var db = postgres.NewContext();
        db.Products.Add(Product.Create(styleId, "Another", "d", 2, 3, 4, "another-" + Guid.NewGuid().ToString("N")[..8], [1], [4], null));

        var error = await RefusedAsync(() => db.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    [Trait("Spec", "URL-6")]
    public async Task ASlugThatIsNotLowerCaseHyphenated_IsRefusedByTheDatabase()
    {
        using var host = new CatalogHost(postgres);
        var styleId = await host.NewStyleAsync();
        await using var db = postgres.NewContext();
        db.Products.Add(Product.Create(styleId, "Bad", "d", 2, 3, 4, "Bad Slug", [1], [4], null));

        var error = await RefusedAsync(() => db.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    [Trait("Spec", "URL-6")]
    public async Task TheSameSlugInTheSameGenderAndCategory_IsRefused_ButDifferentOnesMayShareIt()
    {
        using var host = new CatalogHost(postgres);
        var slug = "shared-" + Guid.NewGuid().ToString("N")[..8];
        await using (var db = postgres.NewContext())
        {
            db.Products.Add(Product.Create(await host.NewStyleAsync(), "One", "d", 2, 3, 4, slug, [1], [4], null));
            await db.SaveChangesAsync();
        }

        await using (var db = postgres.NewContext())
        {
            db.Products.Add(Product.Create(await host.NewStyleAsync(), "Two", "d", 1, 3, 4, slug, [1], [4], null));
            await db.SaveChangesAsync(); // a different gender: allowed
        }

        await using (var db = postgres.NewContext())
        {
            db.Products.Add(Product.Create(await host.NewStyleAsync(), "Three", "d", 2, 3, 4, slug, [1], [4], null));
            var error = await RefusedAsync(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        }
    }

    [Fact]
    [Trait("Spec", "URL-11")]
    public async Task AnAddressInTheHistory_CannotBeTakenByAnotherProduct()
    {
        using var host = new CatalogHost(postgres);
        var first = await host.NewProductAsync();
        var second = await host.NewProductAsync();
        await using var db = postgres.NewContext();
        db.PathHistory.Add(new PathHistoryEntry(first.Id, 2, 3, "old-" + first.Id));
        await db.SaveChangesAsync();
        db.PathHistory.Add(new PathHistoryEntry(second.Id, 2, 3, "old-" + first.Id));

        var error = await RefusedAsync(() => db.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    [Trait("Spec", "PRD-8")]
    public async Task ANonPositivePrice_AndACompareAtNotAbovePrice_AreRefusedByTheDatabase()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await using var db = postgres.NewContext();

        var zero = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_VRNT\" SET \"PRIC\" = 0 WHERE \"PRDT_ID\" = {0}", product.Id));
        var inverted = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_VRNT\" SET \"PRIC\" = 1000, \"CMPR_PRIC\" = 900 WHERE \"PRDT_ID\" = {0}", product.Id));
        var compareWithoutPrice = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_VRNT\" SET \"PRIC\" = NULL, \"CMPR_PRIC\" = 900 WHERE \"PRDT_ID\" = {0}", product.Id));

        Assert.All([zero, inverted, compareWithoutPrice], e => Assert.Equal(PostgresErrorCodes.CheckViolation, e.SqlState));
    }

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task AVariantTripleCannotBeRepeated()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await using var db = postgres.NewContext();

        var error = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CTLG\".\"PRDT_VRNT\" (\"PRDT_ID\", \"CLR_ID\", \"SIZE_ID\", \"INSR_DTE\", \"INSR_BY\") VALUES ({0}, 1, 4, now(), 'x')", product.Id));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    [Trait("Spec", "PRD-7")]
    public async Task PriceHistory_RefusesUpdateAndDelete_ByAnyRoute()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1500m, 1800m)], product.Version, "Test Person"));
        await using var db = postgres.NewContext();
        Assert.True(await db.PriceHistory.AnyAsync(h => h.NewPrice == 1500m));

        var update = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync("UPDATE \"CTLG\".\"PRDT_PRIC_HIST\" SET \"NEW_PRIC\" = 1"));
        var delete = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"CTLG\".\"PRDT_PRIC_HIST\""));

        Assert.Contains("append-only", update.MessageText, StringComparison.Ordinal);
        Assert.Contains("append-only", delete.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-5")]
    public async Task APublishedProductsImage_CannotLoseItsAltText_ByAnyRoute()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        await host.SendAsync(new Romp.Modules.Catalog.Application.Products.PublishProductCommand(product.Id, product.Version, "Test Person"));
        await using var db = postgres.NewContext();

        var cleared = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_IMG\" SET \"ALT\" = NULL WHERE \"PRDT_ID\" = {0}", product.Id));
        var blank = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_IMG\" SET \"ALT\" = '   ' WHERE \"PRDT_ID\" = {0}", product.Id));
        var inserted = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CTLG\".\"PRDT_IMG\" (\"PRDT_ID\", \"FILE_KEY\", \"CNTT_TYP\", \"FILE_NAME\", \"FILE_BYT\", \"WDTH\", \"HGHT\", \"SORT_SEQ\", \"INSR_DTE\", \"INSR_BY\") "
            + "VALUES ({0}, 'k', 'image/png', 'f.png', 10, 1, 1, 9, now(), 'x')", product.Id));

        Assert.Contains("alt text", cleared.MessageText, StringComparison.Ordinal);
        Assert.NotNull(blank);
        Assert.Contains("alt text", inserted.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-5")]
    public async Task AProductCannotMoveToPublished_WhileAnImageHasNoAltText()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await host.SendAsync(new Romp.Modules.Catalog.Application.ProductMedia.UploadProductImageCommand(product.Id, TestImages.Png(5, 5), "a.png", null, null, "Test Person"));
        await using var db = postgres.NewContext();

        var error = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_MAIN\" SET \"PRDT_STS_ID\" = 2 WHERE \"ID\" = {0}", product.Id));

        Assert.Contains("alt text", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-3")]
    public async Task AltTextOver125Characters_IsRefusedByTheDatabase()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await host.SendAsync(new Romp.Modules.Catalog.Application.ProductMedia.UploadProductImageCommand(product.Id, TestImages.Png(5, 5), "a.png", null, null, "Test Person"));
        await using var db = postgres.NewContext();

        var error = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"PRDT_IMG\" SET \"ALT\" = {0} WHERE \"PRDT_ID\" = {1}", new string('x', 126), product.Id));

        Assert.NotNull(error);
    }

    [Fact]
    [Trait("Spec", "CMP-6")]
    public async Task Composition_RefusesPercentagesOutOfRange_AndTheSameFibreTwice()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await using var db = postgres.NewContext();
        db.CompositionLines.Add(new CompositionLine(product.Id, 1, 100.5m));
        var range = await RefusedAsync(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.CompositionLines.AddRange(new CompositionLine(product.Id, 1, 50m), new CompositionLine(product.Id, 1, 50m));
        var duplicate = await RefusedAsync(() => db.SaveChangesAsync());

        Assert.True(range.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    [Fact]
    [Trait("Spec", "CHT-4")]
    [Trait("Spec", "CHT-5")]
    public async Task ChartLines_RefuseMinAboveMax_NegativeValues_AndTheSameSizeTwice()
    {
        using var host = new CatalogHost(postgres);
        await using var db = postgres.NewContext();
        var chart = new SizeChart(1, (short)(100 + Random.Shared.Next(1, 30_000)), "Test chart", [new SizeChartLine(4, 12, 24, 80, 92, null, null, null, null, null)]);
        db.Charts.Add(chart);
        await db.SaveChangesAsync();

        var inverted = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"CHRT_LINE\" SET \"HGHT_MIN_CM\" = 200, \"HGHT_MAX_CM\" = 100 WHERE \"CHRT_ID\" = {0}", chart.Id));
        var negative = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CTLG\".\"CHRT_LINE\" SET \"CHST_CM\" = -1 WHERE \"CHRT_ID\" = {0}", chart.Id));
        var duplicate = await RefusedAsync(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CTLG\".\"CHRT_LINE\" (\"CHRT_ID\", \"SIZE_ID\", \"AGE_MIN_MON\", \"INSR_DTE\", \"INSR_BY\") VALUES ({0}, 4, 1, now(), 'x')", chart.Id));

        Assert.Equal(PostgresErrorCodes.CheckViolation, inverted.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, negative.SqlState);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    [Fact]
    [Trait("Spec", "CHT-5")]
    public async Task OnlyOneChartPerCategoryAndGender()
    {
        using var host = new CatalogHost(postgres);
        var gender = (short)(100 + Random.Shared.Next(30_001, 32_000));
        await using var db = postgres.NewContext();
        db.Charts.Add(new SizeChart(1, gender, "One", [new SizeChartLine(4, 12, 24, null, null, null, null, null, null, null)]));
        await db.SaveChangesAsync();
        db.Charts.Add(new SizeChart(1, gender, "Two", [new SizeChartLine(4, 12, 24, null, null, null, null, null, null, null)]));

        var error = await RefusedAsync(() => db.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public async Task TheStatusLookup_HasTheFourStatuses_WithTheEnumIds()
    {
        await using var db = postgres.NewContext();

        var rows = await db.Set<ProductStatusLookup>().OrderBy(s => s.Id).Select(s => new { s.Id, s.Code }).ToListAsync();

        Assert.Equal(
            [((short)1, "DRAFT"), ((short)2, "PUBLISHED"), ((short)3, "UNPUBLISHED"), ((short)4, "ARCHIVED")],
            rows.Select(r => (r.Id, r.Code)).ToArray());
    }
}
