using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Application.ProductMedia;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.ProductMedia;

[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class ProductImageCommandTests(CatalogPostgres postgres)
{
    private static Task<ProductImageDto> UploadAsync(CatalogHost host, long productId, byte[]? bytes = null, string name = "front.png", short? colour = null, string? alt = null) =>
        host.SendAsync(new UploadProductImageCommand(productId, bytes ?? TestImages.Png(40, 60), name, colour, alt, "Test Person"));

    [Fact]
    [Trait("Spec", "MED-10")]
    [Trait("Spec", "MED-9")]
    public async Task AnUpload_StoresTheOriginal_WithDimensions_AndAGeneratedKey()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var image = await UploadAsync(host, product.Id, TestImages.Jpeg(1200, 800), "../../Weird Name (1).JPG");

        Assert.Equal((1200, 800), (image.Width, image.Height));
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.True(image.IsMain);
        Assert.Null(image.AltText);
        Assert.Equal("Weird Name (1).JPG", image.FileName);
        await using var db = postgres.NewContext();
        var stored = await db.Images.SingleAsync(i => i.Id == image.Id);
        Assert.Matches("^[0-9a-f]{32}$", stored.FileKey);
        Assert.DoesNotContain("Weird", stored.FileKey, StringComparison.Ordinal);
        Assert.True(host.Storage.Files.ContainsKey(stored.FileKey));
    }

    [Fact]
    [Trait("Spec", "MED-7")]
    public async Task AFileThatIsNotAnImage_IsRefused_WhateverItIsCalled()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            UploadAsync(host, product.Id, System.Text.Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>"), "photo.jpg"));

        Assert.Contains("JPEG, PNG or WebP", error.Message, StringComparison.Ordinal);
        Assert.Empty(host.Storage.Files);
        await using var db = postgres.NewContext();
        Assert.False(await db.Images.AnyAsync(i => i.ProductId == product.Id));
    }

    [Fact]
    [Trait("Spec", "MED-7")]
    public async Task AnImageOverTheSizeLimit_IsRefused_InPlainWords()
    {
        using var host = new CatalogHost(postgres, o => o.MaxImageBytes = 2048);
        var product = await host.NewProductAsync();
        var big = TestImages.Png(10, 10).Concat(new byte[4096]).ToArray();

        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => UploadAsync(host, product.Id, big));

        Assert.Contains("limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-7")]
    public async Task AnAcceptedTypeThatIsNotAllowed_IsRefused()
    {
        using var host = new CatalogHost(postgres, o => o.AllowedImageTypes = ["jpeg"]);
        var product = await host.NewProductAsync();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => UploadAsync(host, product.Id));
        Assert.NotNull(await UploadAsync(host, product.Id, TestImages.Jpeg(20, 20), "a.jpg"));
    }

    [Fact]
    [Trait("Spec", "MED-7")]
    public async Task TheEleventhImage_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        for (var i = 0; i < 10; i++)
        {
            await UploadAsync(host, product.Id, name: $"{i}.png");
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => UploadAsync(host, product.Id));

        Assert.Contains("at most 10", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-9")]
    public async Task AStorageFailure_LeavesNoDatabaseRow()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        host.Storage.FailOnSave = true;

        await Assert.ThrowsAsync<IOException>(() => UploadAsync(host, product.Id));

        await using var db = postgres.NewContext();
        Assert.False(await db.Images.AnyAsync(i => i.ProductId == product.Id));
    }

    [Fact]
    public async Task AColourImage_MustBeOneOfTheProductsColours()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync(await host.NewStyleAsync(colours: [1, 2]));

        var ok = await UploadAsync(host, product.Id, colour: 1);
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => UploadAsync(host, product.Id, colour: 3));

        Assert.Equal((short)1, ok.ColourId);
    }

    [Fact]
    [Trait("Spec", "MED-4")]
    public async Task TheAnswerCarriesASuggestedAltText_ButStoresNone()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync(name: "Suggest Dress");

        var image = await UploadAsync(host, product.Id, colour: 1);

        Assert.Equal("Suggest Dress in Red, view 1", image.SuggestedAltText);
        Assert.NotNull(image.AltTextProblem);
        Assert.Null(image.AltText);
    }

    [Fact]
    [Trait("Spec", "MED-2")]
    public async Task SetImageAlt_AcceptsRealText_AndRefusesFileNamesAndLongText()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var image = await UploadAsync(host, product.Id);

        var set = await host.SendAsync(new SetImageAltCommand(image.Id, "  A red floral summer dress, front view ", "Test Person"));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new SetImageAltCommand(image.Id, "IMG_1234.jpg", "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new SetImageAltCommand(image.Id, new string('a', 126), "Test Person")));

        Assert.Equal("A red floral summer dress, front view", set.AltText);
        Assert.Null(set.AltTextProblem);
    }

    [Fact]
    [Trait("Spec", "MED-1")]
    public async Task APublishWithAnImageLackingAltText_NamesThatImage()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        await UploadAsync(host, ready.Id, name: "IMG_1234.jpg");

        var result = await host.SendAsync(new PublishProductCommand(ready.Id, (await host.SendAsync(new GetProductQuery(ready.Id))).Version, "Test Person"));

        Assert.False(result.Published);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("ALT_TEXT_MISSING", failure.Code);
        Assert.Contains("Image 2 (IMG_1234.jpg)", failure.Message, StringComparison.Ordinal);
        Assert.Equal("images", failure.Tab);
    }

    [Fact]
    [Trait("Spec", "MED-6")]
    public async Task AProductWithNoImage_CannotBePublished()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var image = Assert.Single(await host.SendAsync(new ListProductImagesQuery(ready.Id)));
        await host.SendAsync(new DeleteImageCommand(image.Id, "Test Person"));

        var result = await host.SendAsync(new PublishProductCommand(ready.Id, (await host.SendAsync(new GetProductQuery(ready.Id))).Version, "Test Person"));

        Assert.Equal(["NO_IMAGE"], result.Failures.Select(f => f.Code).ToArray());
    }

    [Fact]
    [Trait("Spec", "MED-8")]
    public async Task Reorder_ChangesTheOrderReturned_AndTheFirstIsTheMainImage()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var a = await UploadAsync(host, product.Id, name: "a.png");
        var b = await UploadAsync(host, product.Id, name: "b.png");
        var c = await UploadAsync(host, product.Id, name: "c.png");

        var reordered = await host.SendAsync(new ReorderImagesCommand(product.Id, [c.Id, a.Id, b.Id], "Test Person"));

        Assert.Equal([c.Id, a.Id, b.Id], reordered.Select(i => i.Id).ToArray());
        Assert.True(reordered[0].IsMain);
        Assert.Equal([c.Id, a.Id, b.Id], (await host.SendAsync(new ListProductImagesQuery(product.Id))).Select(i => i.Id).ToArray());
    }

    [Fact]
    [Trait("Spec", "MED-8")]
    public async Task Reorder_MustListExactlyTheProductsImages()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var other = await host.NewProductAsync();
        var a = await UploadAsync(host, product.Id);
        var b = await UploadAsync(host, product.Id);
        var foreign = await UploadAsync(host, other.Id);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ReorderImagesCommand(product.Id, [a.Id], "Test Person")));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ReorderImagesCommand(product.Id, [a.Id, a.Id], "Test Person")));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ReorderImagesCommand(product.Id, [a.Id, foreign.Id], "Test Person")));
        Assert.Equal(2, (await host.SendAsync(new ReorderImagesCommand(product.Id, [b.Id, a.Id], "Test Person"))).Count);
    }

    [Fact]
    public async Task Delete_RemovesTheRowAndThenTheFile()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var image = await UploadAsync(host, product.Id);
        string key;
        await using (var db = postgres.NewContext())
        {
            key = await db.Images.Where(i => i.Id == image.Id).Select(i => i.FileKey).SingleAsync();
        }

        await host.SendAsync(new DeleteImageCommand(image.Id, "Test Person"));

        await using var after = postgres.NewContext();
        Assert.False(await after.Images.AnyAsync(i => i.Id == image.Id));
        Assert.False(host.Storage.Files.ContainsKey(key));
    }

    [Fact]
    public async Task Delete_IsRefusedWhenItWouldLeaveAPublishedProductWithoutAnImage()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));
        var image = Assert.Single(await host.SendAsync(new ListProductImagesQuery(ready.Id)));

        var error = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DeleteImageCommand(image.Id, "Test Person")));

        Assert.Contains("at least one image", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-5")]
    public async Task OnAPublishedProduct_AnUploadNeedsAltText_AndAnAltCannotBeCleared()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));
        var existing = Assert.Single(await host.SendAsync(new ListProductImagesQuery(ready.Id)));

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => UploadAsync(host, ready.Id, name: "back.png"));
        var added = await UploadAsync(host, ready.Id, name: "back.png", alt: "The back of the red floral dress");
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new SetImageAltCommand(existing.Id, null, "Test Person")));

        Assert.Equal("The back of the red floral dress", added.AltText);
    }

    [Fact]
    [Trait("Spec", "PRD-3")]
    public async Task ChangingAnImageOfAPublishedProduct_TellsTheStorefront()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));

        await UploadAsync(host, ready.Id, name: "side.png", alt: "The side of the red floral dress");

        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Set<Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage>().CountAsync(m => m.EventType == "ProductChangedEvent" && m.AggregateId == ready.Id));
    }

    [Fact]
    public async Task AnArchivedProduct_CannotTakeNewImages()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await host.SendAsync(new ArchiveProductCommand(product.Id, product.Version, "Test Person"));

        await Assert.ThrowsAsync<DomainException>(() => UploadAsync(host, product.Id));
    }

    [Fact]
    [Trait("Spec", "MED-9")]
    public async Task TheFile_IsServedWithOurRecordOfItsType()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var image = await UploadAsync(host, product.Id, TestImages.Jpeg(20, 20), "evil.html");

        var file = await host.SendAsync(new GetImageFileQuery(image.Id));

        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal(TestImages.Jpeg(20, 20), file.Content);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetImageFileQuery(long.MaxValue)));
    }
}
