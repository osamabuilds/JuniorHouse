using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
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

public sealed record MenuAudienceDto(string Code, string Name, string Path, IReadOnlyList<MenuEntryDto> Entries);

public sealed record MenuDto(
    IReadOnlyList<MenuAudienceDto> Audiences,
    IReadOnlyList<MenuEntryDto> Collections,
    bool HasNewIn,
    bool HasSale) : IPublicStorefrontResponse
{
    internal static MenuDto From(MenuTree tree) => new(
        [.. tree.Audiences.Select(a => new MenuAudienceDto(a.Code, a.Name, a.Path, [.. a.Entries.Select(Entry)]))],
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
        return MenuDto.From(tree);
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
