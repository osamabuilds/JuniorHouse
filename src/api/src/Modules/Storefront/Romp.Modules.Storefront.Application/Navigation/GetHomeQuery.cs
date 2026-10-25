using MediatR;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;

namespace Romp.Modules.Storefront.Application.Navigation;

/// <summary>An audience tile on the home page: a link to the audience listing.</summary>
public sealed record HomeAudienceDto(string Code, string Name, string Path);

/// <summary>
/// What the home page shows (NAV-10): up to twelve New Arrivals, newest first, and a tile for each audience. <see cref="IsFallback"/> is true
/// when nothing was published inside the window and the newest listable products are shown instead; with no listable product at all the
/// list is empty and the page omits its hero.
/// </summary>
public sealed record HomeDto(
    IReadOnlyList<ListingTileDto> NewArrivals,
    bool IsFallback,
    IReadOnlyList<HomeAudienceDto> Audiences) : IPublicStorefrontResponse;

public sealed record GetHomeQuery : IRequest<HomeDto>;

public sealed class GetHomeQueryHandler(ISender sender) : IRequestHandler<GetHomeQuery, HomeDto>
{
    public const int TileCount = 12;

    public async Task<HomeDto> Handle(GetHomeQuery request, CancellationToken cancellationToken)
    {
        var filter = ListingFilter.Parse(null, null, null, null, null, null, TileCount.ToString(System.Globalization.CultureInfo.InvariantCulture)).Filter!;

        var fresh = (await sender.Send(new SearchListingQuery(ListingScope.NewIn(null), filter), cancellationToken)).Value!.Tiles;
        var isFallback = fresh.Count == 0;
        var tiles = isFallback
            ? (await sender.Send(new SearchListingQuery(ListingScope.Everything(), filter), cancellationToken)).Value!.Tiles
            : fresh;

        var menu = await sender.Send(new GetMenuQuery(), cancellationToken);
        return new HomeDto(tiles, isFallback, [.. menu.Audiences.Select(a => new HomeAudienceDto(a.Code, a.Name, a.Path))]);
    }
}
