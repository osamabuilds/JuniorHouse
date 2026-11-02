using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.Totals;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Cart.Application.Totals;

/// <summary>One city's delivery fee as the admin sees it.</summary>
public sealed record ShippingRateDto(short CityId, string CityName, string ProvinceName, decimal Fee);

/// <summary>The shop-wide delivery settings as the admin sees them. <see cref="DefaultFeeIsUnset"/> is true while the seeded zero is still there.</summary>
public sealed record ShippingSettingsDto(decimal DefaultFee, decimal? FreeThreshold, decimal? CodFee, bool DefaultFeeIsUnset);

/// <summary>Set (add or change) the fee for one city (TOT-5). The city must exist in the reference data.</summary>
public sealed record SetShippingRateCommand(short CityId, decimal Fee) : IRequest<ShippingRateDto>, ICartCommand;

/// <summary>Take a city's own fee away, so it uses the default fee again. Removing a city with no fee of its own is not an error.</summary>
public sealed record RemoveShippingRateCommand(short CityId) : IRequest, ICartCommand;

/// <summary>Change the default fee, the free-delivery amount and the Cash on Delivery fee.</summary>
public sealed record SetShippingSettingsCommand(decimal DefaultFee, decimal? FreeThreshold, decimal? CodFee) : IRequest<ShippingSettingsDto>, ICartCommand;

public sealed record ListShippingRatesQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<ShippingRateDto>>;

public sealed record GetShippingSettingsQuery : IRequest<ShippingSettingsDto>;

public sealed class SetShippingRateCommandHandler(ICartDbContext dbContext, IGeographyQueries geography) : IRequestHandler<SetShippingRateCommand, ShippingRateDto>
{
    public async Task<ShippingRateDto> Handle(SetShippingRateCommand request, CancellationToken cancellationToken)
    {
        var cities = await geography.GetCitiesByIdAsync([request.CityId], cancellationToken);
        if (!cities.TryGetValue(request.CityId, out var city))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["cityId"] = ["That city is not in the list of cities we deliver to."] });
        }

        var existing = await dbContext.ShippingRates.FirstOrDefaultAsync(r => r.CityId == request.CityId, cancellationToken);
        if (existing is null)
        {
            dbContext.ShippingRates.Add(new ShippingCityRate(request.CityId, request.Fee));
        }
        else
        {
            existing.SetFee(request.Fee);
        }

        var provinces = await geography.GetProvincesByIdAsync([city.ProvinceId], cancellationToken);
        return new ShippingRateDto(city.Id, city.Name, provinces.TryGetValue(city.ProvinceId, out var p) ? p.Name : string.Empty, OrderTotals.Round(request.Fee));
    }
}

public sealed class RemoveShippingRateCommandHandler(ICartDbContext dbContext) : IRequestHandler<RemoveShippingRateCommand>
{
    public async Task Handle(RemoveShippingRateCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ShippingRates.FirstOrDefaultAsync(r => r.CityId == request.CityId, cancellationToken);
        if (existing is not null)
        {
            dbContext.ShippingRates.Remove(existing);
        }
    }
}

public sealed class SetShippingSettingsCommandHandler(ICartDbContext dbContext) : IRequestHandler<SetShippingSettingsCommand, ShippingSettingsDto>
{
    public async Task<ShippingSettingsDto> Handle(SetShippingSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await dbContext.ShippingSettings.SingleAsync(cancellationToken);
        settings.Set(request.DefaultFee, request.FreeThreshold, request.CodFee);
        return new ShippingSettingsDto(settings.DefaultFee, settings.FreeThreshold, settings.CodFee, DefaultFeeIsUnset: false);
    }
}

public sealed class ListShippingRatesQueryHandler(ICartDbContext dbContext, IGeographyQueries geography) : IRequestHandler<ListShippingRatesQuery, PagedResult<ShippingRateDto>>
{
    public async Task<PagedResult<ShippingRateDto>> Handle(ListShippingRatesQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.ShippingRates.AsNoTracking().OrderBy(r => r.CityId)
            .Select(r => new { r.CityId, r.Fee })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        // Names come from Reference in two bulk calls for the whole page (no N+1, ADR 0002).
        var cities = await geography.GetCitiesByIdAsync([.. page.Items.Select(i => i.CityId)], cancellationToken);
        var provinces = await geography.GetProvincesByIdAsync([.. cities.Values.Select(c => c.ProvinceId).Distinct()], cancellationToken);
        var items = page.Items.Select(i =>
        {
            cities.TryGetValue(i.CityId, out var city);
            var province = city is not null && provinces.TryGetValue(city.ProvinceId, out var p) ? p.Name : string.Empty;
            return new ShippingRateDto(i.CityId, city?.Name ?? $"City {i.CityId}", province, i.Fee);
        }).ToList();
        return new PagedResult<ShippingRateDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetShippingSettingsQueryHandler(ICartDbContext dbContext) : IRequestHandler<GetShippingSettingsQuery, ShippingSettingsDto>
{
    public async Task<ShippingSettingsDto> Handle(GetShippingSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = await dbContext.ShippingSettings.AsNoTracking().SingleAsync(cancellationToken);
        var unset = s.DefaultFee == 0m && s.FreeThreshold is null && s.CodFee is null && s.UpdtDte is null;
        return new ShippingSettingsDto(s.DefaultFee, s.FreeThreshold, s.CodFee, unset);
    }
}

/// <summary>
/// Reads the current rates and settings into the pure <see cref="ShippingPolicy"/>: two queries however many cities have a rate (TOT-5). A
/// change made in the admin applies from the next read; placed orders keep the fee they were placed with.
/// </summary>
public sealed class ShippingPolicyLoader(ICartDbContext dbContext)
{
    public async Task<ShippingPolicy> LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.ShippingSettings.AsNoTracking().SingleAsync(cancellationToken);
        var rates = await dbContext.ShippingRates.AsNoTracking().ToDictionaryAsync(r => (int)r.CityId, r => r.Fee, cancellationToken);
        return new ShippingPolicy(rates, settings.DefaultFee, settings.FreeThreshold, settings.CodFee);
    }
}
