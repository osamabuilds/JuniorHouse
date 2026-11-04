using MediatR;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>
/// A published product as a small card: what a signed-out shopper's wishlist shows for the products their browser remembers. Only public
/// facts, the same a listing tile shows. A product that is not published is simply absent from the answer.
/// </summary>
public sealed record ProductCardDto(
    long ProductId, string Name, string Path, decimal Price, decimal? WasPrice, bool IsAvailable, string? ImageUrl, string? ImageFallbackUrl, int? ImageWidth);

/// <summary>The cards for up to <see cref="MaxIds"/> product ids, in two bulk calls however many are asked for.</summary>
public sealed record GetProductCardsQuery(IReadOnlyList<long> ProductIds) : IRequest<IReadOnlyList<ProductCardDto>>
{
    public const int MaxIds = 50;
}

public sealed class GetProductCardsQueryHandler(IStorefrontCatalogQueries catalog, IStorefrontImageQueries images)
    : IRequestHandler<GetProductCardsQuery, IReadOnlyList<ProductCardDto>>
{
    /// <summary>A 640-wide file is sharp on a phone and in a desktop grid.</summary>
    public const int ThumbnailMinWidth = 640;

    public async Task<IReadOnlyList<ProductCardDto>> Handle(GetProductCardsQuery request, CancellationToken cancellationToken)
    {
        var ids = request.ProductIds.Where(id => id > 0).Distinct().Take(GetProductCardsQuery.MaxIds).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var products = await catalog.GetProductSummariesAsync(ids, cancellationToken);
        var pictures = (await images.GetThumbnailsAsync(
            products.Where(p => p.MainImageId is not null).Select(p => p.MainImageId!.Value).ToArray(), ThumbnailMinWidth, cancellationToken))
            .ToDictionary(t => t.ImageId);

        // Answer in the order asked, so the page shows the newest tap first.
        return ids.Select(id => products.FirstOrDefault(p => p.ProductId == id)).OfType<ProductSummary>().Select(p =>
        {
            ImageThumbnail? picture = p.MainImageId is { } imageId && pictures.TryGetValue(imageId, out var found) ? found : null;
            return new ProductCardDto(p.ProductId, p.Name, p.Path, p.Price, p.WasPrice, p.IsAvailable, picture?.WebpUrl, picture?.JpegUrl, picture?.Width);
        }).ToList();
    }
}
