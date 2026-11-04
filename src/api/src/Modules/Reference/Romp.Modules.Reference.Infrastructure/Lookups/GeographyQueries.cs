using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Reference.Domain.Lookups.Vendors;
using Romp.Modules.Reference.Infrastructure.Persistence;

namespace Romp.Modules.Reference.Infrastructure.Lookups;

/// <summary>The REF side of the geography contract (customers/addresses ADD-4, ADD-7). Read-only; every method is one SQL statement.</summary>
public sealed class GeographyQueries(ReferenceDbContext dbContext) : IGeographyQueries
{
    public async Task<IReadOnlyList<ProvinceInfo>> GetProvincesAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<ProvinceLookup>().AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortSeq).ThenBy(p => p.Name)
            .Select(p => new ProvinceInfo(p.Id, p.Code, p.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CityInfo>?> GetCitiesAsync(short provinceId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<ProvinceLookup>().AsNoTracking().AnyAsync(p => p.Id == provinceId && p.IsActive, cancellationToken))
        {
            return null;
        }

        return await dbContext.Set<CityLookup>().AsNoTracking()
            .Where(c => c.IsActive && c.ProvinceId == provinceId)
            .OrderBy(c => c.SortSeq).ThenBy(c => c.Name)
            .Select(c => new CityInfo(c.Id, c.Code, c.Name, provinceId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CityBelongsToProvinceAsync(short cityId, short provinceId, CancellationToken cancellationToken) =>
        await dbContext.Set<CityLookup>().AsNoTracking()
            .AnyAsync(
                c => c.Id == cityId && c.IsActive && c.ProvinceId == provinceId
                    && dbContext.Set<ProvinceLookup>().Any(p => p.Id == provinceId && p.IsActive),
                cancellationToken);

    public async Task<IReadOnlyDictionary<short, CityInfo>> GetCitiesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<short, CityInfo>();
        }

        return await dbContext.Set<CityLookup>().AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.ProvinceId != null)
            .Select(c => new CityInfo(c.Id, c.Code, c.Name, c.ProvinceId!.Value))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<short, ProvinceInfo>> GetProvincesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<short, ProvinceInfo>();
        }

        return await dbContext.Set<ProvinceLookup>().AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new ProvinceInfo(p.Id, p.Code, p.Name))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }
}
