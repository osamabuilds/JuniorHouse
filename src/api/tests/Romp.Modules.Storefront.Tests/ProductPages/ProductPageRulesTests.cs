using Romp.Modules.Storefront.Domain.ProductPages;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Tests.ProductPages;

/// <summary>Product pages task 1: path, selection, price and gallery rules (PDP-2, 3, 9, 13, 15).</summary>
public sealed class ProductPageRulesTests
{
    private static VariantFact V(short colourId, string colour, string size, AvailabilityClass a, decimal? price = 1000m, decimal? compare = null) =>
        new(colourId, colour, size, a, price, compare);

    [Theory]
    [Trait("Spec", "PDP-13")]
    [InlineData("/girls/dresses/floral", "/girls/dresses/floral", false)]
    [InlineData("/Girls/Dresses/Floral", "/girls/dresses/floral", true)]
    [InlineData("/girls/dresses/floral/", "/girls/dresses/floral", true)]
    [InlineData("girls/dresses/floral", "/girls/dresses/floral", true)]
    public void APath_IsNormalised_AndSaysWhetherItChanged(string raw, string expected, bool changed)
    {
        var result = ProductPath.Normalise(raw);

        Assert.Equal(new NormalisedPath(expected, changed), result);
    }

    [Theory]
    [Trait("Spec", "PDP-13")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/a/../etc/passwd")]
    [InlineData("/a/./b")]
    [InlineData("/a//b")]
    [InlineData("/a/b?x=1")]
    [InlineData("/a/b#top")]
    [InlineData("/a\\b")]
    [InlineData("/a/b c")]
    [InlineData("/a/b\u0000")]
    public void AHostilePath_IsRefused(string? raw) => Assert.Null(ProductPath.Normalise(raw));

    [Fact]
    [Trait("Spec", "PDP-13")]
    public void ThePathLengthCap_IsTwoHundred()
    {
        Assert.NotNull(ProductPath.Normalise("/" + new string('a', ProductPath.MaxLength - 1)));
        Assert.Null(ProductPath.Normalise("/" + new string('a', ProductPath.MaxLength)));
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public void TheDefaultColour_IsTheFirstWithASizeThatCanBeBought()
    {
        var variants = new[]
        {
            V(1, "navy", "2y", AvailabilityClass.SoldOut),
            V(1, "navy", "3y", AvailabilityClass.SoldOut),
            V(2, "red", "2y", AvailabilityClass.LowStock),
        };

        var selection = ProductSelection.Resolve(variants, null, null);

        Assert.Equal(new PageSelection(2, "red", null), selection);
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public void WhenNothingCanBeBought_TheFirstColourIsChosen()
    {
        var variants = new[] { V(1, "navy", "2y", AvailabilityClass.SoldOut), V(2, "red", "2y", AvailabilityClass.NotYetAvailable) };

        Assert.Equal(1, ProductSelection.Resolve(variants, null, null)!.ColourId);
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public void AnUnknownColour_IsIgnored_AndAKnownOneIsMatchedWithoutCase()
    {
        var variants = new[] { V(1, "navy", "2y", AvailabilityClass.InStock), V(2, "red", "2y", AvailabilityClass.InStock) };

        Assert.Equal(1, ProductSelection.Resolve(variants, "purple", null)!.ColourId);
        Assert.Equal(2, ProductSelection.Resolve(variants, "RED", null)!.ColourId);
    }

    [Fact]
    [Trait("Spec", "PDP-3")]
    public void ASize_CountsOnlyWithinTheChosenColour_AndAnUnknownOneIsIgnored()
    {
        var variants = new[] { V(1, "navy", "2y", AvailabilityClass.InStock), V(2, "red", "4y", AvailabilityClass.InStock) };

        Assert.Equal("2y", ProductSelection.Resolve(variants, "navy", "2Y")!.SizeCode);
        Assert.Null(ProductSelection.Resolve(variants, "navy", "4y")!.SizeCode);
        Assert.Null(ProductSelection.Resolve(variants, "navy", "<script>")!.SizeCode);
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public void ACodeWithAnUnderscore_IsMatchedByItsUrlForm()
    {
        var variants = new[] { V(1, "DARK_BLUE", "XS_S", AvailabilityClass.InStock) };

        var selection = ProductSelection.Resolve(variants, "dark-blue", "xs-s");

        Assert.Equal(new PageSelection(1, "DARK_BLUE", "XS_S"), selection);
    }

    [Fact]
    [Trait("Spec", "PDP-3")]
    public void AProductWithoutVariants_HasNoSelection() => Assert.Null(ProductSelection.Resolve([], "navy", "2y"));

    [Fact]
    [Trait("Spec", "PDP-15")]
    public void BeforeASizeIsChosen_TheLowestAvailablePriceIsShownAsFrom_WhenPricesDiffer()
    {
        var variants = new[]
        {
            V(1, "navy", "2y", AvailabilityClass.SoldOut, 800m),
            V(1, "navy", "3y", AvailabilityClass.InStock, 1000m),
            V(1, "navy", "4y", AvailabilityClass.InStock, 1200m),
        };

        var price = ProductPricing.For(variants, new PageSelection(1, "navy", null));

        Assert.Equal(new DisplayedPrice(1000m, null, true), price);
    }

    [Fact]
    [Trait("Spec", "PDP-15")]
    public void ASizeChoice_ShowsThatVariantsOwnPrice()
    {
        var variants = new[] { V(1, "navy", "3y", AvailabilityClass.InStock, 1000m), V(1, "navy", "4y", AvailabilityClass.InStock, 1200m) };

        var price = ProductPricing.For(variants, new PageSelection(1, "navy", "4y"));

        Assert.Equal(new DisplayedPrice(1200m, null, false), price);
    }

    [Fact]
    [Trait("Spec", "PDP-9")]
    public void CompareAt_IsShownOnlyWhenAboveThePrice()
    {
        var variants = new[]
        {
            V(1, "navy", "3y", AvailabilityClass.InStock, 1000m, 1500m),
            V(1, "navy", "4y", AvailabilityClass.InStock, 1000m, 1000m),
            V(1, "navy", "5y", AvailabilityClass.InStock, 1000m, 900m),
        };

        Assert.Equal(1500m, ProductPricing.For(variants, new PageSelection(1, "navy", "3y"))!.CompareAtPrice);
        Assert.Null(ProductPricing.For(variants, new PageSelection(1, "navy", "4y"))!.CompareAtPrice);
        Assert.Null(ProductPricing.For(variants, new PageSelection(1, "navy", "5y"))!.CompareAtPrice);
    }

    [Fact]
    [Trait("Spec", "PDP-15")]
    public void WhenNothingCanBeBought_ThePriceStillShows_AndUnpricedVariantsNeverCount()
    {
        var sold = new[] { V(1, "navy", "3y", AvailabilityClass.SoldOut, 900m), V(1, "navy", "4y", AvailabilityClass.NotYetAvailable, null) };
        var none = new[] { V(1, "navy", "3y", AvailabilityClass.NotYetAvailable, null) };

        Assert.Equal(900m, ProductPricing.For(sold, new PageSelection(1, "navy", null))!.Price);
        Assert.Null(ProductPricing.For(none, new PageSelection(1, "navy", null)));
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public void AColoursOwnImages_OverrideTheProductLevelOnes_InSortOrder()
    {
        var images = new[] { new ImageFact(1, null, 1), new ImageFact(2, 5, 2), new ImageFact(3, 5, 1), new ImageFact(4, 6, 1) };

        Assert.Equal([3L, 2L], GalleryImages.For(images, 5).Select(i => i.Id));
    }

    [Fact]
    [Trait("Spec", "PDP-2")]
    public void AColourWithoutImages_ShowsTheProductLevelOnes_NeverAnotherColoursAndTiesBreakById()
    {
        var images = new[] { new ImageFact(9, null, 1), new ImageFact(2, null, 1), new ImageFact(4, 6, 1) };

        Assert.Equal([2L, 9L], GalleryImages.For(images, 5).Select(i => i.Id));
    }
}
