using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;

namespace Romp.Modules.Catalog.Application.Styles;

/// <summary>Search/list for the Styles admin screen - filter by code/name, category and active flag.</summary>
public sealed record SearchStylesQuery(string? SearchText = null, short? CategoryId = null, bool ActiveOnly = true)
    : IRequest<IReadOnlyList<StyleSummaryDto>>;

public sealed class SearchStylesQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<SearchStylesQuery, IReadOnlyList<StyleSummaryDto>>
{
    public async Task<IReadOnlyList<StyleSummaryDto>> Handle(SearchStylesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Styles.AsNoTracking().AsQueryable();

        if (request.ActiveOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(s => s.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.Trim().ToLowerInvariant();

            // CA1304/CA1311/CA1862 don't apply: this is an expression tree EF translates to SQL
            // (LOWER()/LIKE), not an in-memory string comparison - the StringComparison overloads
            // they suggest aren't translatable by EF Core.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(s => s.Code.ToLower().Contains(searchText) || s.Name.ToLower().Contains(searchText));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var styles = await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);

        return styles.Select(s => s.ToSummaryDto()).ToList();
    }
}
