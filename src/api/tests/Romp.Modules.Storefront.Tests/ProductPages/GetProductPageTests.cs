using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.ProductPages;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.ProductPages;

/// <summary>Product pages tasks 3 to 7: <see cref="GetProductPageQuery"/> against a real PostgreSQL (PDP-1, 2, 4..8, 13, 16, 17).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class GetProductPageTests(StorefrontPostgres postgres)
{
    private const string Path = "/girls/dresses/floral-1";

    private static PublishedVariant V(long id, string colour, string size, string? sku, decimal? price = 1000m, decimal? compareAt = null, bool sellable = true) =>
        new(id, new PublishedCode(colour == "RED" ? (short)1 : (short)2, colour, colour == "RED" ? "Red" : "Navy"),
            new PublishedCode(size == "5-6Y" ? (short)9 : (short)4, size, size), sku, price, compareAt, sellable);

    /// <summary>Publishes a product the way the projection would, with stock per variant id, and makes Catalog resolve its path.</summary>
    private static async Task PublishAsync(
        StorefrontHost host, IReadOnlyList<PublishedVariant> variants, Dictionary<long, int>? stock = null, IReadOnlyList<PublishedImage>? images = null)
    {
        host.Storage.Files["images/a.jpg"] = [1, 2, 3];
        var snapshot = Snapshots.Product(id: 1, path: Path, variants: variants, images: images ?? [new PublishedImage(101, null, "images/a.jpg", 1200, 1600, "Red floral dress", 1)]);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, snapshot));
        foreach (var v in variants.Where(v => v.SkuCode is not null))
        {
            await host.SendAsync(new ApplyAvailabilityCommand(new StockAvailability(v.VariantId, v.SkuCode!, stock?.GetValueOrDefault(v.VariantId, 10) ?? 10, 1)));
        }

        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }

        host.Paths.Answers[Path] = new PathResolutionResult(PathOutcome.Current, 1, Path);
    }

    private static async Task<ProductPageDto> PageAsync(StorefrontHost host, string? colour = null, string? size = null)
    {
        var result = await host.SendAsync(new GetProductPageQuery(Path, colour, size));
        Assert.Equal("Found", result.Outcome);
        return result.Page!;
    }

    private static PublishedSizeChart Chart(long id, string gender) =>
        new(id, 1, new PublishedCode(2, "DRESSES", "Dresses"), new PublishedCode(id == 1 ? (short)1 : (short)3, gender, gender), $"{gender} chart",
            [new PublishedSizeChartLine(new PublishedCode(9, "5-6Y", "5-6 Years"), 60, 83, 110m, 122m, null, null, 60m, 55m, 70m),
             new PublishedSizeChartLine(new PublishedCode(4, "7-8Y", "7-8 Years"), 84, 107, 122m, 134m, null, null, 64m, 58m, 74m)]);

    [Fact]
    [Trait("Spec", "PDP-1")]
    public async Task APublishedProduct_ShowsItsNameBreadcrumbPriceVariantsGalleryCompositionAndCare()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1"), V(12, "RED", "7-8Y", "SKU-2", 1200m)]);

        var page = await PageAsync(host);

        Assert.Equal(("Floral dress", Path, "A dress."), (page.Name, page.Path, page.Description));
        Assert.Equal(["Home", "Girls", "Dresses", "Floral dress"], page.Breadcrumbs.Select(b => b.Label).ToArray());
        Assert.Null(page.Breadcrumbs[^1].Path);
        Assert.Equal(new ProductPriceDto(1000m, null, 0, true), page.Price);
        Assert.Equal(["5-6y", "7-8y"], page.Sizes.Select(s => s.Code).ToArray());
        Assert.Null(page.Sku);
        var image = Assert.Single(page.Images);
        Assert.NotEmpty(image.Sources);
        Assert.All(image.Sources, s => Assert.StartsWith("/media/", s.Webp, StringComparison.Ordinal));
        Assert.Equal(("Cotton", 100m), (page.Composition[0].Name, page.Composition[0].Percent));
        Assert.Equal(("Wash at 30", "wash-30", "Wash separately"), (page.Care[0].Name, page.Care[0].SymbolCode, page.CareNote));
        Assert.True(page.IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PDP-3")]
    public async Task AChosenSize_ShowsItsSkuAndItsOwnPrice_AndHighlightsTheChartRow()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1"), V(12, "RED", "7-8Y", "SKU-2", 1200m, 1500m)]);
        await host.SendAsync(new ApplySizeChartCommand(1, Chart(1, "GIRLS")));

        var page = await PageAsync(host, "red", "7-8y");

        Assert.Equal(("SKU-2", "7-8y"), (page.Sku, page.SelectedSize));
        Assert.Equal(new ProductPriceDto(1200m, 1500m, 20, false), page.Price);
        Assert.Equal(["7-8Y"], page.SizeChart!.Rows.Where(r => r.Selected).Select(r => r.SizeCode).ToArray());
    }

    [Fact]
    [Trait("Spec", "PDP-4")]
    public async Task ASizeWithNoStock_IsSoldOutAndNotSelectable_AndLowStockStaysSelectable()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1"), V(12, "RED", "7-8Y", "SKU-2")], new() { [11] = 0, [12] = 3 });

        var page = await PageAsync(host);

        Assert.Equal(("SoldOut", false), (page.Sizes[0].Availability, page.Sizes[0].Selectable));
        Assert.Equal(("LowStock", true), (page.Sizes[1].Availability, page.Sizes[1].Selectable));
    }

    [Fact]
    [Trait("Spec", "PDP-5")]
    public async Task AVariantWithoutAGtinYet_IsListedAsNotYetAvailable()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1"), V(12, "RED", "7-8Y", null, null, null, sellable: false)]);

        var page = await PageAsync(host);

        Assert.Equal(("NotYetAvailable", false), (page.Sizes[1].Availability, page.Sizes[1].Selectable));
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public async Task AColoursOwnImages_ReplaceTheProductLevelOnes_AndColoursShowWhetherTheyHaveStock()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Storage.Files["images/navy.jpg"] = [4, 5, 6];
        await PublishAsync(
            host,
            [V(11, "RED", "5-6Y", "SKU-1", 1000m), V(12, "NAVY", "5-6Y", "SKU-2", 1100m)],
            new() { [11] = 5, [12] = 0 },
            [new PublishedImage(101, null, "images/a.jpg", 1200, 1600, "Product level", 1), new PublishedImage(102, 2, "images/navy.jpg", 1200, 1600, "Navy only", 1)]);

        var red = await PageAsync(host, "red");
        var navy = await PageAsync(host, "navy");

        Assert.Equal(["Product level"], red.Images.Select(i => i.AltText).ToArray());
        Assert.Equal(["Navy only"], navy.Images.Select(i => i.AltText).ToArray());
        Assert.Equal(1100m, navy.Price!.Price);
        Assert.Equal([("red", true), ("navy", false)], navy.Colours.Select(c => (c.Code.ToLowerInvariant(), c.InStock)).OrderByDescending(c => c.Item2).ToArray());
    }

    [Fact]
    [Trait("Spec", "PDP-6")]
    public async Task TheChart_IsTheProductsOwn_ElseTheCategorysUnisexOne()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1")]);
        await host.SendAsync(new ApplySizeChartCommand(2, Chart(2, "UNISEX")));

        Assert.Equal("UNISEX chart", (await PageAsync(host)).SizeChart!.Name);

        await host.SendAsync(new ApplySizeChartCommand(1, Chart(1, "GIRLS")));

        Assert.Equal("GIRLS chart", (await PageAsync(host)).SizeChart!.Name);
    }

    [Fact]
    [Trait("Spec", "PDP-7")]
    public async Task WithoutAChart_ThePageStillWorks()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1")]);

        var page = await PageAsync(host);

        Assert.Null(page.SizeChart);
        Assert.NotEmpty(page.Sizes);
    }

    [Fact]
    [Trait("Spec", "PDP-16")]
    public async Task AProductWithNothingInStock_IsStillFound_AndSaysItIsNotAvailable()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1", 900m)], new() { [11] = 0 });

        var page = await PageAsync(host);

        Assert.False(page.IsAvailable);
        Assert.Equal("SoldOut", page.Sizes[0].Availability);
        Assert.Equal(900m, page.Price!.Price);
    }

    [Fact]
    [Trait("Spec", "PDP-13")]
    public async Task AnOldSlug_AnUpperCasePath_AndATrailingSlash_Redirect_InOneHopToCatalogsPath()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1")]);
        host.Paths.Answers["/girls/dresses/old-name"] = new PathResolutionResult(PathOutcome.Moved, 1, Path);

        foreach (var request in new[] { "/girls/dresses/old-name", "/Girls/Dresses/Floral-1", Path + "/" })
        {
            var result = await host.SendAsync(new GetProductPageQuery(request, null, null));

            Assert.Equal(("Moved", Path, null), (result.Outcome, result.Path, result.Page?.Name));
        }
    }

    [Fact]
    [Trait("Spec", "PDP-13")]
    public async Task AnArchivedProductIsGone_AnUnknownOrUnpublishedPathAndHostilePathsAreNotFound()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1")]);
        host.Paths.Answers["/girls/dresses/archived"] = new PathResolutionResult(PathOutcome.Gone, 7, null);

        Assert.Equal("Gone", (await host.SendAsync(new GetProductPageQuery("/girls/dresses/archived", null, null))).Outcome);
        foreach (var path in new[] { "/girls/dresses/unknown", "/girls/dresses/../../etc/passwd", null, "", new string('a', 500) })
        {
            var result = await host.SendAsync(new GetProductPageQuery(path, null, null));

            Assert.Equal(("NotFound", null), (result.Outcome, result.Path));
        }
    }

    [Fact]
    [Trait("Spec", "PDP-13")]
    public async Task APublishedProductNotProjectedYet_IsNotFound_AndCounted()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Paths.Answers[Path] = new PathResolutionResult(PathOutcome.Current, 99, Path);

        var result = await host.SendAsync(new GetProductPageQuery(Path, null, null));

        Assert.Equal("NotFound", result.Outcome);
        Assert.Equal(1, host.PageMetrics.NotProjected);
    }

    [Fact]
    [Trait("Spec", "PDP-17")]
    public async Task TheSerialisedPage_NeverCarriesAnExactStockQuantity()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, [V(11, "RED", "5-6Y", "SKU-1")], new() { [11] = 7391 });

        var json = System.Text.Json.JsonSerializer.Serialize(await host.SendAsync(new GetProductPageQuery(Path, null, "5-6y")));

        Assert.DoesNotContain("7391", json, StringComparison.Ordinal);
    }
}
