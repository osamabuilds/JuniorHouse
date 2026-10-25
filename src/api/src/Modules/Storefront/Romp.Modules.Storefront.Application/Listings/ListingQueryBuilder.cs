using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

#pragma warning disable CA1304, CA1311 // ToLower() here is translated to SQL lower() (EF Core has no ToLowerInvariant translation); the database collation decides, not the app culture.

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>
/// The read-model query behind every listing: the products of a scope that are listable, then narrowed by the filter. Filtering and
/// sorting happen in SQL (LST-13); a size, colour and price filter must all hold on the same in-stock variant (Q-LST-9).
/// </summary>
internal static class ListingQueryBuilder
{
    /// <summary>Listable products of the scope (LST-15: nothing in stock is never listed). Not yet narrowed by the filter.</summary>
    public static IQueryable<PublishedProduct> InScope(IStorefrontReadDbContext db, ResolvedScope scope, DateTimeOffset newInCutoff)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsListable);

        if (scope.Gender is { } gender)
        {
            var code = gender.Code;
            products = products.Where(p => p.GenderCode == code);
        }

        if (scope.Category is { } category)
        {
            var code = category.Code;
            products = products.Where(p => p.CategoryCode == code);
        }

        switch (scope.Scope.Kind)
        {
            case ListingScopeKind.NewIn or ListingScopeKind.AllNewIn:
                products = products.Where(p => p.PublishedAt >= newInCutoff);
                break;
            case ListingScopeKind.Sale or ListingScopeKind.AllSale:
                products = products.Where(p => p.IsOnSale);
                break;
            case ListingScopeKind.Collection:
                var collection = scope.Collection!.Code;
                products = products.Where(p => db.Collections.Any(c => c.ProductId == p.Id && c.CollectionCode == collection));
                break;
        }

        return products;
    }

    /// <summary>The in-stock variants that satisfy the size, colour and price filters together. With no filters: every in-stock variant.</summary>
    public static IQueryable<VariantRow> MatchingVariants(IStorefrontReadDbContext db, ListingFilter filter)
    {
        var variants = db.Variants.AsNoTracking().Where(v => v.IsSellable && v.AvailableQuantity > 0);

        if (filter.Sizes.Count > 0)
        {
            var sizes = filter.Sizes.ToList();
            variants = variants.Where(v => sizes.Contains(v.SizeCode.ToLower().Replace("_", "-")));
        }

        if (filter.Colours.Count > 0)
        {
            var colours = filter.Colours.ToList();
            variants = variants.Where(v => colours.Contains(v.ColourCode.ToLower().Replace("_", "-")));
        }

        if (filter.PriceMin is { } min)
        {
            variants = variants.Where(v => v.Price >= min);
        }

        if (filter.PriceMax is { } max)
        {
            variants = variants.Where(v => v.Price <= max);
        }

        return variants;
    }

    /// <summary>The scope's products that also have at least one matching variant and satisfy the age filter.</summary>
    public static IQueryable<PublishedProduct> Matching(IQueryable<PublishedProduct> inScope, IQueryable<VariantRow> matching, ListingFilter filter)
    {
        var products = inScope;
        if (filter.Age is { } age)
        {
            products = products.Where(p => p.AgeCode.ToLower().Replace("_", "-") == age);
        }

        return filter.HasVariantFilters
            ? products.Where(p => matching.Any(v => v.ProductId == p.Id))
            : products;
    }
}
