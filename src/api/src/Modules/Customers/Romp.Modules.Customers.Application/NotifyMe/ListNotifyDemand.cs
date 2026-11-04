using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Application.NotifyMe;

/// <summary>
/// One row of the staff demand list (NTM-11): how many people are waiting for a variant. Counts only: there is no address, name or any
/// other personal data in this type, so none can reach the screen, the response or a log (NTM-12).
/// </summary>
public sealed record NotifyDemandItem(
    long VariantId, string ProductName, string ColourName, string SizeName, string? SkuCode, int Waiting, DateTimeOffset OldestRequestAt);

/// <summary>
/// The Notify Me demand list (NTM-11, NTM-13): variants with open requests, most waiting first. One grouped query over this feature's own
/// table (a count and a page), then one bulk call to Storefront for the names of the variants on the page, so the cost does not grow with
/// the number of requests or rows. The page size is clamped to 1..100.
/// </summary>
public sealed record ListNotifyDemandQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<NotifyDemandItem>>;

public sealed class ListNotifyDemandQueryHandler(ICustomerDbContext dbContext, IStorefrontCatalogQueries catalog)
    : IRequestHandler<ListNotifyDemandQuery, PagedResult<NotifyDemandItem>>
{
    public const string UnknownProduct = "(no longer published)";

    private sealed record Demand(long VariantId, int Waiting, DateTimeOffset Oldest);

    public async Task<PagedResult<NotifyDemandItem>> Handle(ListNotifyDemandQuery request, CancellationToken cancellationToken)
    {
        var open = NotifyStatus.Open;
        var paging = new PageRequest(request.Page, request.PageSize).Normalized();

        // Statement 1: how many variants have waiting people. Statement 2: the page of groups, most waiting first, ties by variant id.
        var total = await dbContext.NotifyRequests.AsNoTracking().Where(r => r.Status == open).Select(r => r.VariantId).Distinct().CountAsync(cancellationToken);
        var groups = await dbContext.NotifyRequests.AsNoTracking()
            .Where(r => r.Status == open)
            .GroupBy(r => r.VariantId)
            .Select(g => new { VariantId = g.Key, Waiting = g.Count(), Oldest = g.Min(r => r.InsrDte) })
            .OrderByDescending(d => d.Waiting)
            .ThenBy(d => d.VariantId)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);
        var page = new PagedResult<Demand>(groups.Select(g => new Demand(g.VariantId, g.Waiting, g.Oldest)).ToList(), paging.Page, paging.PageSize, total);
        if (page.Items.Count == 0)
        {
            return new PagedResult<NotifyDemandItem>([], page.Page, page.PageSize, page.TotalCount);
        }

        var names = (await catalog.GetVariantSummariesAsync(page.Items.Select(d => d.VariantId).ToList(), cancellationToken))
            .ToDictionary(v => v.VariantId);

        var items = page.Items.Select(d => names.TryGetValue(d.VariantId, out var v)
                ? new NotifyDemandItem(d.VariantId, v.ProductName, v.ColourName, v.SizeName, v.SkuCode, d.Waiting, d.Oldest)
                : new NotifyDemandItem(d.VariantId, UnknownProduct, string.Empty, string.Empty, null, d.Waiting, d.Oldest))
            .ToList();
        return new PagedResult<NotifyDemandItem>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
