using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Products;

/// <summary>
/// The content version (CTLG.PRDT_MAIN.CNTT_VER) is the storefront read model's "highest version wins" key (published-catalog plan,
/// PCT-5): it goes up by exactly one with every change that emits an event, and by nothing else.
/// </summary>
public sealed class ProductContentVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static Product NewProduct() => Product.Create(7, "Floral Dress", "A dress.", 2, 3, 4, "floral-dress", [1], [4], 1299m);

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void ANewProduct_StartsAtVersionOne()
    {
        Assert.Equal(1, NewProduct().ContentVersion);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void Publish_BumpsOnce_AndPublishingAgainChangesNothing()
    {
        var product = NewProduct();

        product.Publish("A B", Now, "/g/c/s");
        product.Publish("A B", Now, "/g/c/s");

        Assert.Equal(2, product.ContentVersion);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void Unpublish_Bumps()
    {
        var product = NewProduct();
        product.Publish("A B", Now, "/g/c/s");

        product.Unpublish("/g/c/s");

        Assert.Equal(3, product.ContentVersion);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void ArchivingAPublishedProduct_BumpsOnce_EvenThoughItRaisesTwoEvents()
    {
        var product = NewProduct();
        product.Publish("A B", Now, "/g/c/s");

        product.Archive("/g/c/s");
        product.Archive("/g/c/s");

        Assert.Equal(3, product.ContentVersion);
        Assert.Equal(3, product.DomainEvents.Count);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void NoteChanged_BumpsOnlyWhenItRaisesAnEvent()
    {
        var product = NewProduct();
        Assert.False(product.NoteChanged("/g/c/s"));
        Assert.Equal(1, product.ContentVersion);

        product.Publish("A B", Now, "/g/c/s");
        Assert.True(product.NoteChanged("/g/c/s"));
        Assert.True(product.NoteChanged("/g/c/s"));

        Assert.Equal(4, product.ContentVersion);
    }
}

/// <summary>The bump is saved in the same transaction as the change (real PostgreSQL).</summary>
[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class ProductContentVersionDatabaseTests(CatalogPostgres postgres)
{
    private async Task<long> VersionOfAsync(long productId)
    {
        await using var db = postgres.NewContext();
        return await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.ContentVersion).SingleAsync();
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task TheVersionIsSavedWithThePublish_AndWithEveryLaterChangeToAPublishedProduct()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        Assert.Equal(1, await VersionOfAsync(product.Id));

        await host.SendAsync(new PublishProductCommand(product.Id, product.Version, "Test Person"));
        var afterPublish = await VersionOfAsync(product.Id);

        var current = await host.SendAsync(new GetProductQuery(product.Id));
        await host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1500m, 1800m)], current.Version, "Test Person"));

        Assert.Equal(2, afterPublish);
        Assert.Equal(3, await VersionOfAsync(product.Id));
    }
}
