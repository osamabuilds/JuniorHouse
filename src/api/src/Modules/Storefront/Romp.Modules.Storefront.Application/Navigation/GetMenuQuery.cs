using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Navigation;

namespace Romp.Modules.Storefront.Application.Navigation;

/// <summary>Menu configuration, behind a port so Application never reads configuration itself.</summary>
public interface IMenuSettings
{
    /// <summary>Gender lookup codes that are not header entries (owner, 2026-10-24: Unisex).</summary>
    IReadOnlySet<string> HiddenGenderCodes { get; }
}

/// <summary>The header menu, built from lookup rows and the read model (NAV-1, 3, 5, 8, 9; owner decision 2026-10-25).</summary>
public sealed record GetMenuQuery : IRequest<MenuDto>;

/// <summary>One menu link on the wire. <see cref="Kind"/> is <c>new-in</c>, <c>sale</c>, <c>category</c>, <c>collection</c> or <c>age</c>.</summary>
public sealed record MenuEntryDto(string Kind, string Code, string Label, string Path);

/// <summary>
/// The picture tile in an audience's mega menu panel (MGT-1): the newest listable product's main picture, with a fixed eyebrow and line, linking
/// to the audience's New in page (or the audience page when nothing is new). Derived, so no admin setting is needed.
/// </summary>
public sealed record MenuTileDto(string Eyebrow, string Line, string Path, ListingImageDto Image);

public sealed record MenuAudienceDto(string Code, string Name, string Path, IReadOnlyList<MenuEntryDto> Entries, MenuTileDto? Tile = null);

public sealed record MenuDto(
    IReadOnlyList<MenuAudienceDto> Audiences,
    IReadOnlyList<MenuEntryDto> Collections,
    bool HasNewIn,
    bool HasSale) : IPublicStorefrontResponse
{
    internal static MenuDto From(MenuTree tree, IReadOnlyDictionary<string, MenuTileDto> tiles) => new(
        [.. tree.Audiences.Select(a => new MenuAudienceDto(a.Code, a.Name, a.Path, [.. a.Entries.Select(Entry)], tiles.GetValueOrDefault(a.Code)))],
        [.. tree.Collections.Select(Entry)],
        tree.HasNewIn,
        tree.HasSale);

    private static MenuEntryDto Entry(MenuEntry e) => new(
        e.Kind switch
        {
            MenuEntryKind.NewIn => "new-in",
            MenuEntryKind.Sale => "sale",
            MenuEntryKind.Category => "category",
            MenuEntryKind.Collection => "collection",
            MenuEntryKind.Age => "age",
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.Kind, "Unknown menu entry kind."),
        },
        e.Code,
        e.Label,
        e.Path);
}

public sealed class GetMenuQueryHandler(
    IStorefrontReadDbContext db,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache cache,
    IMenuSettings settings) : IRequestHandler<GetMenuQuery, MenuDto>
{
    public async Task<MenuDto> Handle(GetMenuQuery request, CancellationToken cancellationToken)
    {
        var rows = await cache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);
        var availability = await ReadAvailabilityAsync(cancellationToken);
        var tree = MenuBuilder.Build(rows.Genders, rows.Categories, rows.Collections, rows.Ages, availability, settings.HiddenGenderCodes);
        var tiles = await ReadTilesAsync(tree, cancellationToken);
        return MenuDto.From(tree, tiles);
    }

    /// <summary>
    /// One tile per audience that has a listable product with a processed picture: a grouped query for the newest listable product of each
    /// audience, then the two picture queries for all of them at once, however many audiences there are (MGT-1, MGT-3).
    /// </summary>
    private async Task<Dictionary<string, MenuTileDto>> ReadTilesAsync(MenuTree tree, CancellationToken cancellationToken)
    {
        if (tree.Audiences.Count == 0)
        {
            return [];
        }

        var newest = await db.Products.AsNoTracking()
            .Where(p => p.IsListable)
            .GroupBy(p => p.GenderCode)
            .Select(g => new { Gender = g.Key, ProductId = g.OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id).Select(p => p.Id).First() })
            .ToListAsync(cancellationToken);
        if (newest.Count == 0)
        {
            return [];
        }

        var images = await ListingTiles.ImagesAsync(db, [.. newest.Select(n => n.ProductId)], cancellationToken);
        var tiles = new Dictionary<string, MenuTileDto>(StringComparer.Ordinal);
        foreach (var audience in tree.Audiences)
        {
            var product = newest.FirstOrDefault(n => string.Equals(n.Gender, audience.Code, StringComparison.Ordinal));
            if (product is null || !images.TryGetValue(product.ProductId, out var image) || image.Sources.Count == 0)
            {
                continue;
            }

            var path = audience.Entries.FirstOrDefault(e => e.Kind == MenuEntryKind.NewIn)?.Path ?? audience.Path;
            tiles[audience.Code] = new MenuTileDto("New in", "Just arrived", path, image);
        }

        return tiles;
    }


    /// <summary>Two grouped queries however many entries the menu has: product scopes, then collection scopes.</summary>
    private async Task<MenuAvailability> ReadAvailabilityAsync(CancellationToken cancellationToken)
    {
        var scopes = await db.Products.AsNoTracking()
            .Where(p => p.IsListable)
            .GroupBy(p => new { p.GenderCode, p.CategoryCode, p.AgeCode, p.IsOnSale })
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        var collectionScopes = await db.Collections.AsNoTracking()
            .Join(db.Products.Where(p => p.IsListable), c => c.ProductId, p => p.Id, (c, p) => new { p.GenderCode, c.CollectionCode })
            .Distinct()
            .ToListAsync(cancellationToken);

        return new MenuAvailability
        {
            Genders = scopes.Select(s => s.GenderCode).ToHashSet(StringComparer.Ordinal),
            Categories = scopes.Select(s => (s.GenderCode, s.CategoryCode)).ToHashSet(),
            Ages = scopes.Select(s => (s.GenderCode, s.AgeCode)).ToHashSet(),
            SaleGenders = scopes.Where(s => s.IsOnSale).Select(s => s.GenderCode).ToHashSet(StringComparer.Ordinal),
            Collections = collectionScopes.Select(c => (c.GenderCode, c.CollectionCode)).ToHashSet(),
        };
    }
}
