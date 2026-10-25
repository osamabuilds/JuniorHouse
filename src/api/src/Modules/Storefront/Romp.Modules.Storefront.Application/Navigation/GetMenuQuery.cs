using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.Navigation;

namespace Romp.Modules.Storefront.Application.Navigation;

/// <summary>Menu configuration, behind a port so Application never reads configuration itself.</summary>
public interface IMenuSettings
{
    /// <summary>Gender lookup codes that are not header entries (owner, 2026-10-24: Unisex).</summary>
    IReadOnlySet<string> HiddenGenderCodes { get; }
}

/// <summary>The four lookup lists the menu is built from.</summary>
public sealed record MenuLookups(
    IReadOnlyList<MenuLookup> Genders,
    IReadOnlyList<MenuLookup> Categories,
    IReadOnlyList<MenuLookup> Collections,
    IReadOnlyList<MenuLookup> Ages);

/// <summary>
/// The 60-second, single-flight copy of the lookups (plan: a cache stampede must not turn into a burst of Reference queries). One entry per
/// process; a concurrent caller waits for the load already running.
/// </summary>
public interface IMenuLookupCache
{
    Task<MenuLookups> GetOrLoadAsync(Func<CancellationToken, Task<MenuLookups>> load, CancellationToken cancellationToken);
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
    IMenuLookupCache cache,
    IMenuSettings settings) : IRequestHandler<GetMenuQuery, MenuDto>
{
    public async Task<MenuDto> Handle(GetMenuQuery request, CancellationToken cancellationToken)
    {
        var rows = await cache.GetOrLoadAsync(LoadAsync, cancellationToken);
        var availability = await ReadAvailabilityAsync(cancellationToken);
        var tree = MenuBuilder.Build(rows.Genders, rows.Categories, rows.Collections, rows.Ages, availability, settings.HiddenGenderCodes);
        return MenuDto.From(tree);
    }

    private async Task<MenuLookups> LoadAsync(CancellationToken cancellationToken)
    {
        // Sequential: the lookups share one Reference context per scope.
        var genders = await lookups.ListGendersAsync(cancellationToken);
        var categories = await lookups.ListCategoriesAsync(cancellationToken);
        var collections = await lookups.ListCollectionsAsync(cancellationToken);
        var ages = await lookups.ListAgeBracketsAsync(cancellationToken);
        return new MenuLookups(Map(genders), Map(categories), Map(collections), Map(ages));
    }

    private static List<MenuLookup> Map(IReadOnlyList<CatalogLookupItem> items) =>
        items.Select(i => new MenuLookup(i.Code, i.Name, i.SortSeq, i.IsActive)).ToList();

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
