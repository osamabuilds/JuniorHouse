using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Tests.Products;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static Product NewProduct(decimal? price = 1299m) =>
        Product.Create(7, " Floral Dress ", " A dress. ", 2, 3, 4, "floral-dress", [1, 2], [4, 5, 6], price);

    [Fact]
    [Trait("Spec", "PRD-1")]
    public void Create_PlansOneVariantForEveryColourAndSize()
    {
        var product = NewProduct();

        Assert.Equal(6, product.Variants.Count);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal("Floral Dress", product.Name);
        Assert.Equal(6, product.Variants.Select(v => (v.ColourId, v.SizeId)).Distinct().Count());
        Assert.All(product.Variants, v => Assert.Equal(1299m, v.Price));
    }

    [Fact]
    [Trait("Spec", "PRD-10")]
    public void Publish_SetsTheStatus_AndRaisesTheEventWithThePath()
    {
        var product = NewProduct();

        product.Publish("Ayesha Khan", Now, "/girls/dresses/floral-dress");

        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(Now, product.PublishedAt);
        Assert.Equal("Ayesha Khan", product.PublishedBy);
        var published = Assert.IsType<ProductPublishedEvent>(Assert.Single(product.DomainEvents));
        Assert.Equal("/girls/dresses/floral-dress", published.Path);
        Assert.Equal(7, published.StyleId);
    }

    [Fact]
    public void PublishingAPublishedProduct_ChangesNothing()
    {
        var product = NewProduct();
        product.Publish("A B", Now, "/g/c/s");
        product.ClearDomainEvents();

        product.Publish("C D", Now.AddDays(1), "/g/c/s");

        Assert.Empty(product.DomainEvents);
        Assert.Equal("A B", product.PublishedBy);
    }

    [Fact]
    [Trait("Spec", "PRD-3")]
    public void Unpublish_TakesAPublishedProductOffSale_AndRaisesTheEvent()
    {
        var product = NewProduct();
        product.Publish("A B", Now, "/g/c/s");
        product.ClearDomainEvents();

        product.Unpublish("/g/c/s");

        Assert.Equal(ProductStatus.Unpublished, product.Status);
        Assert.IsType<ProductUnpublishedEvent>(Assert.Single(product.DomainEvents));
    }

    [Fact]
    public void Unpublish_RefusesAProductThatIsNotPublished() =>
        Assert.Throws<DomainException>(() => NewProduct().Unpublish("/g/c/s"));

    [Fact]
    [Trait("Spec", "PRD-6")]
    public void Archive_IsFinal_AndNothingCanBeEditedAfterwards()
    {
        var product = NewProduct();

        product.Archive("/g/c/s");

        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.IsType<ProductArchivedEvent>(Assert.Single(product.DomainEvents));
        Assert.Throws<DomainException>(() => product.Publish("A B", Now, "/g/c/s"));
        Assert.Throws<DomainException>(() => product.UpdateContent("x", "y", 2, 3, 4, null));
        Assert.Throws<DomainException>(() => product.SetPrices([(1, 4, 100m, null)], "A B"));
        Assert.Throws<DomainException>(() => product.SetCollections([1]));
    }

    [Fact]
    public void ArchivingAPublishedProduct_AlsoTellsConsumersItWentOffSale()
    {
        var product = NewProduct();
        product.Publish("A B", Now, "/g/c/s");
        product.ClearDomainEvents();

        product.Archive("/g/c/s");

        Assert.Contains(product.DomainEvents, e => e is ProductArchivedEvent);
        Assert.Contains(product.DomainEvents, e => e is ProductUnpublishedEvent);
    }

    [Fact]
    public void ArchivingTwice_ChangesNothing()
    {
        var product = NewProduct();
        product.Archive("/g/c/s");
        product.ClearDomainEvents();

        product.Archive("/g/c/s");

        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    [Trait("Spec", "URL-2")]
    public void APublishedProduct_CannotChangeGenderOrCategoryThroughContentEdits()
    {
        var product = NewProduct();
        product.Publish("A B", Now, "/g/c/s");

        Assert.Throws<DomainException>(() => product.UpdateContent("Floral", "d", 1, 3, 4, null));
        product.UpdateContent("Floral Dress 2", "d", 2, 3, 5, "Wash inside out");
        Assert.Equal("Floral Dress 2", product.Name);
        Assert.Equal("Wash inside out", product.CareNote);
    }

    [Fact]
    public void ChangePath_ReturnsTheOldAndNewTriple()
    {
        var product = NewProduct();

        var change = product.ChangePath(1, 2, "new-slug");

        Assert.True(change.Changed);
        Assert.Equal((short)2, change.OldGenderId);
        Assert.Equal("floral-dress", change.OldSlug);
        Assert.Equal("new-slug", product.Slug);
        Assert.False(product.ChangePath(1, 2, "new-slug").Changed);
    }

    [Fact]
    [Trait("Spec", "PRD-12")]
    public void SetCollections_ReplacesTheSet_WithoutDuplicates()
    {
        var product = NewProduct();

        product.SetCollections([1, 2, 2]);
        product.SetCollections([2, 3]);

        Assert.Equal([2, 3], product.Collections.Select(c => c.CollectionId).Order().ToArray());
    }

    [Fact]
    [Trait("Spec", "PRD-2")]
    public void SetPrices_RefusesAVariantThatIsNotTheProducts()
    {
        var product = NewProduct();

        Assert.Throws<DomainException>(() => product.SetPrices([(9, 9, 100m, null)], "A B"));
    }

    [Fact]
    public void NoteChanged_OnlyTellsTheStorefrontAboutAPublishedProduct()
    {
        var product = NewProduct();
        Assert.False(product.NoteChanged("/g/c/s"));
        Assert.Empty(product.DomainEvents);

        product.Publish("A B", Now, "/g/c/s");
        product.ClearDomainEvents();
        Assert.True(product.NoteChanged("/g/c/new", "/g/c/s"));

        var changed = Assert.IsType<ProductChangedEvent>(Assert.Single(product.DomainEvents));
        Assert.Equal("/g/c/s", changed.PreviousPath);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Wash separately", true)]
    [InlineData("<script>alert(1)</script>", false)]
    public void CareNote_IsPlainText(string? note, bool ok) => Assert.Equal(ok, Product.CheckCareNote(note) is null);

    [Fact]
    public void CareNote_IsLimitedTo500Characters()
    {
        Assert.Null(Product.CheckCareNote(new string('a', 500)));
        Assert.NotNull(Product.CheckCareNote(new string('a', 501)));
    }
}
