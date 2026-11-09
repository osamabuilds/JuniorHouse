namespace Romp.Modules.Reference.Contracts;

/// <summary>A province or territory as other modules may see it.</summary>
public sealed record ProvinceInfo(short Id, string Code, string Name);

/// <summary>A city and the province it belongs to.</summary>
public sealed record CityInfo(short Id, string Code, string Name, short ProvinceId);

/// <summary>
/// Customers (address book) and later checkout read provinces and cities only through this contract (ADR 0002, ADR 0021). Lists offer
/// active rows only; the by-id reads include retired rows so an address saved earlier still shows its names. One query per call.
/// </summary>
public interface IGeographyQueries
{
    /// <summary>Active provinces in display order.</summary>
    Task<IReadOnlyList<ProvinceInfo>> GetProvincesAsync(CancellationToken cancellationToken);

    /// <summary>Active cities of an active province in display order, or null when the province is unknown or retired.</summary>
    Task<IReadOnlyList<CityInfo>?> GetCitiesAsync(short provinceId, CancellationToken cancellationToken);

    /// <summary>True only when the city is active, has a province, and that province is the given active one.</summary>
    Task<bool> CityBelongsToProvinceAsync(short cityId, short provinceId, CancellationToken cancellationToken);

    /// <summary>Names for a set of city ids, active or not; unknown ids are absent.</summary>
    Task<IReadOnlyDictionary<short, CityInfo>> GetCitiesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    /// <summary>Names for a set of province ids, active or not; unknown ids are absent.</summary>
    Task<IReadOnlyDictionary<short, ProvinceInfo>> GetProvincesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);
}
