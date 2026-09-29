using Microsoft.EntityFrameworkCore;

namespace Romp.BuildingBlocks.Application.Paging;

/// <summary>Which page of a list to return. Out-of-range values are clamped, never rejected.</summary>
public readonly record struct PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public PageRequest Normalized() => new(Math.Max(1, Page), Math.Clamp(PageSize, 1, MaxPageSize));

    public int Skip => (Normalized().Page - 1) * Normalized().PageSize;
}

/// <summary>One page of a list plus the total, so a client can render pager controls.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingExtensions
{
    /// <summary>
    /// Two SQL statements however large the table is: a COUNT and a single OFFSET/LIMIT page. The
    /// query must already be ordered by a unique key so pages never overlap or skip rows.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken)
    {
        var page = request.Normalized();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
