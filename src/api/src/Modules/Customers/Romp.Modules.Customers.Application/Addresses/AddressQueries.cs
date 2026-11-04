using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Customers.Application.Addresses;

public sealed record GetAddressQuery(long Id) : IRequest<AddressDto>;

/// <summary>The caller's addresses, default first and then newest, one page at a time (ADD-7).</summary>
public sealed record ListAddressesQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<AddressDto>>;

public sealed class GetAddressQueryHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IGeographyQueries geography)
    : IRequestHandler<GetAddressQuery, AddressDto>
{
    public async Task<AddressDto> Handle(GetAddressQuery request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var address = await dbContext.Addresses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.Id && a.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Address not found.");

        return (await AddressMapper.ToDtosAsync([address], geography, cancellationToken))[0];
    }
}

public sealed class ListAddressesQueryHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IGeographyQueries geography)
    : IRequestHandler<ListAddressesQuery, PagedResult<AddressDto>>
{
    public async Task<PagedResult<AddressDto>> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var page = await dbContext.Addresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.Id)
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var items = await AddressMapper.ToDtosAsync(page.Items, geography, cancellationToken);
        return new PagedResult<AddressDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>Joins province and city names onto a page of addresses with one call per kind, never one per row (ADD-7).</summary>
internal static class AddressMapper
{
    public static async Task<IReadOnlyList<AddressDto>> ToDtosAsync(IReadOnlyCollection<CustomerAddress> addresses, IGeographyQueries geography, CancellationToken cancellationToken)
    {
        var cities = await geography.GetCitiesByIdAsync(addresses.Select(a => a.CityId).Distinct().ToArray(), cancellationToken);
        var provinces = await geography.GetProvincesByIdAsync(addresses.Select(a => a.ProvinceId).Distinct().ToArray(), cancellationToken);

        return addresses.Select(a => new AddressDto(
            a.Id, a.RecipientName, a.Phone,
            a.ProvinceId, provinces.TryGetValue(a.ProvinceId, out var province) ? province.Name : string.Empty,
            a.CityId, cities.TryGetValue(a.CityId, out var city) ? city.Name : string.Empty,
            a.LineOne, a.LineTwo, a.Postcode, a.IsDefault)).ToList();
    }
}
