using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Wishlist;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Application.Wishlist;

/// <summary>
/// One saved product as the wishlist page shows it. A product the storefront no longer publishes keeps its row with <c>Unavailable</c> in
/// <see cref="Notes"/> and no price, so the customer can still remove it (WSL-5). Notes are <c>PriceDropped</c>, <c>OnSale</c>, <c>SoldOut</c>, <c>Unavailable</c>.
/// </summary>
public sealed record WishlistItemDto(
    long ProductId, string? Name, string? Path, decimal? Price, decimal? WasPrice, decimal SavedPrice, bool IsAvailable,
    string? ImageUrl, string? ImageFallbackUrl, int? ImageWidth, IReadOnlyList<string> Notes, DateTimeOffset SavedAt);

/// <summary>The caller's saved products, newest first, one page at a time (WSL-8).</summary>
public sealed record ListWishlistQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<WishlistItemDto>>;

/// <summary>Just the product ids on the caller's list (at most the list limit), for the heart state on product and listing pages (WSL-1).</summary>
public sealed record GetWishlistIdsQuery : IRequest<IReadOnlyList<long>>;

public sealed class ListWishlistQueryHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IStorefrontCatalogQueries catalog, IStorefrontImageQueries images)
    : IRequestHandler<ListWishlistQuery, PagedResult<WishlistItemDto>>
{
    public const int ThumbnailMinWidth = 640;

    public async Task<PagedResult<WishlistItemDto>> Handle(ListWishlistQuery request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var page = await dbContext.Wishlist.AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.Id)
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        // One bulk call for the whole page, never one per item (WSL-8).
        var summaries = (await catalog.GetProductSummariesAsync(page.Items.Select(i => i.ProductId).ToArray(), cancellationToken))
            .ToDictionary(s => s.ProductId);

        // The pictures come in one bulk call too, at the width the card needs (a 640 file is sharp on a phone and a desktop grid).
        var pictures = (await images.GetThumbnailsAsync(
            summaries.Values.Where(p => p.MainImageId is not null).Select(p => p.MainImageId!.Value).ToArray(), ThumbnailMinWidth, cancellationToken))
            .ToDictionary(t => t.ImageId);

        var items = page.Items.Select(item =>
        {
            summaries.TryGetValue(item.ProductId, out var product);
            var notes = WishlistFlags.Evaluate(item.SavedPrice, product?.Price, product?.WasPrice, product?.IsAvailable);
            ImageThumbnail? picture = product?.MainImageId is { } imageId && pictures.TryGetValue(imageId, out var found) ? found : null;
            return new WishlistItemDto(
                item.ProductId, product?.Name, product?.Path, product?.Price, product?.WasPrice, item.SavedPrice, product?.IsAvailable ?? false,
                picture?.WebpUrl, picture?.JpegUrl, picture?.Width, NoteNames(notes), item.InsrDte);
        }).ToList();

        return new PagedResult<WishlistItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static List<string> NoteNames(WishlistNotes notes) =>
        Enum.GetValues<WishlistNotes>().Where(n => n != WishlistNotes.None && notes.HasFlag(n)).Select(n => n.ToString()).ToList();
}

public sealed class GetWishlistIdsQueryHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IOptions<CustomersOptions> options)
    : IRequestHandler<GetWishlistIdsQuery, IReadOnlyList<long>>
{
    public async Task<IReadOnlyList<long>> Handle(GetWishlistIdsQuery request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        return await dbContext.Wishlist.AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.Id)
            .Select(w => w.ProductId)
            .Take(options.Value.MaxWishlistItems)
            .ToListAsync(cancellationToken);
    }
}
