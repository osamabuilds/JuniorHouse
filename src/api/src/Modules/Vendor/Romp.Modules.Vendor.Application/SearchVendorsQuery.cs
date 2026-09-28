using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>Search/list for the Vendors admin screen - filter by name/city/specialisation.</summary>
public sealed record SearchVendorsQuery(
    string? SearchText = null,
    short? CityId = null,
    short? SpecialisationId = null,
    bool ActiveOnly = true) : IRequest<IReadOnlyList<VendorSummaryDto>>;

public sealed class SearchVendorsQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<SearchVendorsQuery, IReadOnlyList<VendorSummaryDto>>
{
    public async Task<IReadOnlyList<VendorSummaryDto>> Handle(SearchVendorsQuery request, CancellationToken cancellationToken)
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

        var vendors = await query.OrderBy(v => v.Name).ToListAsync(cancellationToken);

        return vendors.Select(v => v.ToSummaryDto()).ToList();
    }
}
