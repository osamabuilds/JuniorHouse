using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Tests.Products;

public sealed class SellableVariantsTests
{
    private static Product NewProduct(decimal? price = 1000m) =>
        Product.Create(7, "Dress", "A dress", 2, 3, 4, "dress", [1, 2], [4, 5], price);

    private static LabelledVariant Label(long id, short colour, short size, bool voided = false) =>
        new(id, 7, colour, size, $"SKU-{id}", id.ToString("0000000000000", System.Globalization.CultureInfo.InvariantCulture), voided);

    [Fact]
    [Trait("Spec", "PRD-1")]
    public void EveryPlannedVariant_IsListed_AndOnlyThoseWithAGtinShowTheirSkuAndGtin()
    {
        var product = NewProduct();

        var views = SellableVariants.Join(product.Variants, [Label(1, 1, 4), Label(2, 2, 5)]);

        Assert.Equal(4, views.Count);
        var first = views.Single(v => v.ColourId == 1 && v.SizeId == 4);
        Assert.Equal("SKU-1", first.SkuCode);
        Assert.Equal(VariantAvailability.Sellable, first.Availability);
        var waiting = views.Single(v => v.ColourId == 1 && v.SizeId == 5);
        Assert.Null(waiting.SkuCode);
        Assert.Null(waiting.Gtin);
        Assert.Equal(VariantAvailability.NotYetAvailable, waiting.Availability);
        Assert.Equal(2, views.Count(v => v.IsSellable));
    }

    [Fact]
    [Trait("Spec", "PRD-13")]
    public void ALabelledButUnpricedVariant_IsNotSellableYet()
    {
        var product = NewProduct(price: null);

        var views = SellableVariants.Join(product.Variants, [Label(1, 1, 4)]);

        Assert.Equal(VariantAvailability.Unpriced, views.Single(v => v.ColourId == 1 && v.SizeId == 4).Availability);
        Assert.Equal(0, views.Count(v => v.IsSellable));
    }

    [Fact]
    [Trait("Spec", "PRD-14")]
    public void AVoidedGtin_MakesTheVariantNotSellable()
    {
        var product = NewProduct();

        var views = SellableVariants.Join(product.Variants, [Label(1, 1, 4, voided: true)]);

        Assert.Equal(VariantAvailability.GtinVoided, views.Single(v => v.ColourId == 1 && v.SizeId == 4).Availability);
    }

    [Fact]
    [Trait("Spec", "PRD-14")]
    public void AVoidedGtinThatWasReplaced_IsSellableAgainWithTheNewSku()
    {
        var product = NewProduct();

        var views = SellableVariants.Join(product.Variants, [Label(1, 1, 4, voided: true), Label(9, 1, 4)]);

        var view = views.Single(v => v.ColourId == 1 && v.SizeId == 4);
        Assert.Equal(VariantAvailability.Sellable, view.Availability);
        Assert.Equal("SKU-9", view.SkuCode);
    }

    [Fact]
    [Trait("Spec", "PRD-15")]
    public void TheOrderOfTheLabelledRows_DoesNotChangeTheResult()
    {
        var product = NewProduct();
        var rows = new[] { Label(1, 1, 4, voided: true), Label(9, 1, 4), Label(2, 2, 5) };

        var forward = SellableVariants.Join(product.Variants, rows);
        var backward = SellableVariants.Join(product.Variants, rows.Reverse());

        Assert.Equal(forward, backward);
    }
}
