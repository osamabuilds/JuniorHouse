using MediatR;
using Romp.BuildingBlocks.Application.Paging;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;

namespace Romp.Modules.Vendor.Application.Vendors;

/// <summary>Search/list for the Vendors admin screen - filter by name/city/specialisation.</summary>
public sealed record SearchVendorsQuery(
    string? SearchText = null,
    short? CityId = null,
    short? SpecialisationId = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<VendorSummaryDto>>;

public sealed class SearchVendorsQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<SearchVendorsQuery, PagedResult<VendorSummaryDto>>
{
    public async Task<PagedResult<VendorSummaryDto>> Handle(SearchVendorsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Vendors.AsNoTracking().AsQueryable();

        if (request.ActiveOnly)
        {
            query = query.Where(v => v.IsActive);
        }

        if (request.CityId is { } cityId)
        {
            query = query.Where(v => v.CityId == cityId);
        }

        if (request.SpecialisationId is { } specialisationId)
        {
            query = query.Where(v => v.Specialisations.Any(s => s.SpecialisationId == specialisationId));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(v => v.Name.ToLower().Contains(searchText));
#pragma warning restore CA1304, CA1311, CA1862
        }

        // Projected in SQL (no entities materialised) and ordered by a unique key so pages are stable.
        return await query
            .OrderBy(v => v.Name).ThenBy(v => v.Id)
            .Select(v => new VendorSummaryDto(v.Id, v.Name, v.CityId, v.IsActive))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}
