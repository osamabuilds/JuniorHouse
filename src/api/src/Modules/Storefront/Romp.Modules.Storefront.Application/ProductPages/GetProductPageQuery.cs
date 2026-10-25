using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.ProductPages;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.ProductPages;

/// <summary>The product page for a public path (PDP-1..17): the address is resolved through Catalog, the content comes from the read model.</summary>
public sealed record GetProductPageQuery(string? Path, string? Colour, string? Size) : IRequest<ProductPageResultDto>;

/// <summary>Counts a published product Catalog knows that the read model does not hold yet, so a slow projection shows up (plan).</summary>
public interface IProductPageMetrics
{
    void NotProjectedYet();
}

public sealed class GetProductPageQueryHandler(
    IStorefrontReadDbContext db,
    IProductPathQueries paths,
    IReadModelSettings settings,
    IProductPageMetrics metrics) : IRequestHandler<GetProductPageQuery, ProductPageResultDto>
{
    private const string UnisexSegment = "unisex";

    public async Task<ProductPageResultDto> Handle(GetProductPageQuery request, CancellationToken cancellationToken)
    {
        if (ProductPath.Normalise(request.Path) is not { } path)
        {
            return Result(ProductPageOutcome.NotFound);
        }

        var resolved = await paths.ResolveAsync(path.Value, cancellationToken);
        switch (resolved.Outcome)
        {
            case PathOutcome.Gone:
                return Result(ProductPageOutcome.Gone);
            case PathOutcome.Moved:
                return Result(ProductPageOutcome.Moved, resolved.CurrentPath);
            case PathOutcome.Current when path.Changed:
                return Result(ProductPageOutcome.Moved, resolved.CurrentPath);
            case PathOutcome.Current when resolved.ProductId is { } id:
                return await LoadAsync(id, resolved.CurrentPath ?? path.Value, request, cancellationToken);
            default:
                return Result(ProductPageOutcome.NotFound);
        }
    }

    private async Task<ProductPageResultDto> LoadAsync(long productId, string path, GetProductPageQuery request, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            metrics.NotProjectedYet();
            return Result(ProductPageOutcome.NotFound);
        }

        var variants = await db.Variants.AsNoTracking().Where(v => v.ProductId == productId)
            .OrderBy(v => v.ColourName).ThenBy(v => v.SizeSeq).ThenBy(v => v.Id).ToListAsync(cancellationToken);
        var facts = variants.Select(v => (Row: v, Fact: new VariantFact(
            v.ColourId, v.ColourCode, v.SizeCode, ClassOf(v), v.Price, v.CompareAtPrice))).ToList();
        var factList = facts.Select(f => f.Fact).ToList();
        var selection = ProductSelection.Resolve(factList, request.Colour, request.Size);

        var images = await db.Images.AsNoTracking().Where(i => i.ProductId == productId).ToListAsync(cancellationToken);
        var chosenImages = selection is null
            ? GalleryImages.For(images.Select(Fact), 0)
            : GalleryImages.For(images.Select(Fact), selection.ColourId);
        var imageIds = chosenImages.Select(i => i.Id).ToList();
        var files = await db.Derivatives.AsNoTracking()
            .Where(d => imageIds.Contains(d.ImageId) && d.Status == DerivativeStatus.Done && d.FileKey != null)
            .ToListAsync(cancellationToken);
        var gallery = chosenImages
            .Select(c => images.First(i => i.Id == c.Id))
            .Select(i => ImageDto(i, files.Where(d => d.ImageId == i.Id).ToList()))
            .Where(i => i.Sources.Count > 0)
            .ToList();

        var composition = await db.Composition.AsNoTracking().Where(c => c.ProductId == productId)
            .OrderBy(c => c.SortSeq).ThenBy(c => c.Id).ToListAsync(cancellationToken);
        var care = await db.CareInstructions.AsNoTracking().Where(c => c.ProductId == productId)
            .OrderBy(c => c.SortSeq).ThenBy(c => c.Id).ToListAsync(cancellationToken);
        var chart = await ChartAsync(product, selection?.SizeCode, cancellationToken);

        var colours = facts.GroupBy(f => f.Row.ColourCode).Select(g =>
            new ColourOptionDto(CodeSegment.Of(g.Key), g.First().Row.ColourName, g.Any(f => f.Fact.Availability.IsPurchasable()))).ToList();
        var sizes = selection is null
            ? []
            : facts.Where(f => f.Row.ColourId == selection.ColourId).Select(f => new SizeOptionDto(
                CodeSegment.Of(f.Row.SizeCode), f.Row.SizeName, f.Fact.Availability.ToString(), f.Fact.Availability.IsPurchasable())).ToList();
        var price = selection is null ? null : ProductPricing.For(factList, selection);
        var selectedVariant = selection?.SizeCode is null
            ? null
            : variants.FirstOrDefault(v => v.ColourId == selection.ColourId && string.Equals(v.SizeCode, selection.SizeCode, StringComparison.OrdinalIgnoreCase));

        var gender = new MenuAudienceRef(product.GenderCode, product.GenderName);
        var crumbs = BreadcrumbTrail.For(gender, new MenuCategoryRef(product.CategoryCode, product.CategoryName), product.Name)
            .Select(c => new BreadcrumbDto(c.Label, c.Path)).ToList();

        var page = new ProductPageDto(
            path,
            product.Name,
            product.Description,
            product.GenderName,
            product.CategoryName,
            product.AgeName,
            crumbs,
            price is null ? null : new ProductPriceDto(price.Price, price.CompareAtPrice, Saving(price), price.IsFrom, SavingAmount(price)),
            selection is null ? string.Empty : CodeSegment.Of(selection.ColourCode),
            selection?.SizeCode is { } chosen ? CodeSegment.Of(chosen) : null,
            selectedVariant?.SkuCode,
            colours,
            sizes,
            gallery,
            chart,
            composition.Select(c => new CompositionLineDto(c.FibreName, c.Percent)).ToList(),
            care.Select(c => new CareLineDto(c.CareName, c.SymbolCode)).ToList(),
            product.CareNote,
            factList.Any(f => f.Availability.IsPurchasable()));
        return new ProductPageResultDto(ProductPageOutcome.Found.ToString(), null, page);
    }

    private AvailabilityClass ClassOf(VariantRow v) => Availability.Classify(v.IsSellable, v.AvailableQuantity, settings.LowStockThreshold);

    private static ImageFact Fact(ImageRow i) => new(i.Id, i.ColourId, i.SortSeq);

    private static int Saving(DisplayedPrice price) =>
        price.CompareAtPrice is > 0 and var was && was > price.Price ? (int)Math.Round((was - price.Price) / was * 100m) : 0;

    private static decimal SavingAmount(DisplayedPrice price) =>
        price.CompareAtPrice is > 0 and var was && was > price.Price ? was - price.Price : 0m;

    private static ProductImageDto ImageDto(ImageRow image, List<DerivativeRow> files) =>
        new(image.AltText, image.Width, image.Height,
            files.GroupBy(d => d.Width).OrderBy(g => g.Key)
                .Select(g => new ProductImageSourceDto(
                    g.Key,
                    "/media/" + (g.FirstOrDefault(d => d.Format == DerivativeFormat.Webp)?.FileKey ?? string.Empty),
                    g.FirstOrDefault(d => d.Format == DerivativeFormat.Jpeg) is { } jpeg ? "/media/" + jpeg.FileKey : null))
                .Where(s => s.Webp != "/media/")
                .ToList());

    /// <summary>The chart for the product's category and gender, else the category's Unisex chart, else none (PDP-6, 7). Two queries.</summary>
    private async Task<SizeChartDto?> ChartAsync(PublishedProduct product, string? selectedSize, CancellationToken cancellationToken)
    {
        var candidates = await db.Charts.AsNoTracking().Where(c => c.CategoryCode == product.CategoryCode).ToListAsync(cancellationToken);
        var chart = candidates.FirstOrDefault(c => c.GenderCode == product.GenderCode)
            ?? candidates.FirstOrDefault(c => CodeSegment.Of(c.GenderCode) == UnisexSegment);
        if (chart is null)
        {
            return null;
        }

        var lines = await db.ChartLines.AsNoTracking().Where(l => l.ChartId == chart.Id)
            .OrderBy(l => l.SizeSeq).ThenBy(l => l.Id).ToListAsync(cancellationToken);
        return new SizeChartDto(chart.Name, lines.Select(l => new SizeChartRowDto(
            l.SizeCode, l.SizeName, l.AgeMinMonths, l.AgeMaxMonths, l.HeightMinCm, l.HeightMaxCm, l.WeightMinKg, l.WeightMaxKg,
            l.ChestCm, l.WaistCm, l.LengthCm, selectedSize is not null && string.Equals(l.SizeCode, selectedSize, StringComparison.OrdinalIgnoreCase))).ToList());
    }

    private static ProductPageResultDto Result(ProductPageOutcome outcome, string? path = null) => new(outcome.ToString(), path, null);
}
