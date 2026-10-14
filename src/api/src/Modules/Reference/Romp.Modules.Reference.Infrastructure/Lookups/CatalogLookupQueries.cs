using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Reference.Domain.Lookups;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Infrastructure.Persistence;

namespace Romp.Modules.Reference.Infrastructure.Lookups;

/// <summary>The REF side of the Catalog contract (Sprint 7): codes and names in one query per lookup.</summary>
public sealed class CatalogLookupQueries(ReferenceDbContext dbContext) : ICatalogLookupQueries
{
    public Task<IReadOnlyDictionary<short, ApparelCode>> GetGendersAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<GenderLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetCategoriesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<CategoryLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetAgeBracketsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<AgeBracketLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetCollectionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<CollectionLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetFibresAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<FibreLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<SizeLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Codes<ColourLookup>(ids, cancellationToken);

    public Task<IReadOnlyDictionary<string, short>> GetGenderIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken) =>
        IdsBySegment<GenderLookup>(segments, cancellationToken);

    public Task<IReadOnlyDictionary<string, short>> GetCategoryIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken) =>
        IdsBySegment<CategoryLookup>(segments, cancellationToken);

    private async Task<IReadOnlyDictionary<string, short>> IdsBySegment<TLookup>(IReadOnlyCollection<string> segments, CancellationToken cancellationToken)
        where TLookup : Lookup
    {
        if (segments.Count == 0)
        {
            return new Dictionary<string, short>();
        }

        var wanted = segments.Select(s => s.ToLowerInvariant()).Distinct().ToList();
        var rows = await dbContext.Set<TLookup>().AsNoTracking()
#pragma warning disable CA1304, CA1311 // translated to SQL lower(), which has no culture
            .Where(l => wanted.Contains(l.Code.ToLower().Replace("_", "-")))
#pragma warning restore CA1304, CA1311
            .Select(l => new { l.Id, l.Code })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Code.ToLowerInvariant().Replace('_', '-'), r => r.Id);
    }

    public async Task<IReadOnlyDictionary<short, CareInstructionCode>> GetCareInstructionsAsync(
        IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<short, CareInstructionCode>();
        }

        var wanted = ids.Distinct().ToList();
        return await dbContext.Set<CareInstructionLookup>().AsNoTracking()
            .Where(l => wanted.Contains(l.Id))
            .Select(l => new CareInstructionCode(l.Id, l.Code, l.Name, l.SymbolCode))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<short, ApparelCode>> Codes<TLookup>(IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
        where TLookup : Lookup
    {
        if (ids.Count == 0)
        {
            return new Dictionary<short, ApparelCode>();
        }

        var wanted = ids.Distinct().ToList();
        return await dbContext.Set<TLookup>().AsNoTracking()
            .Where(l => wanted.Contains(l.Id))
            .Select(l => new ApparelCode(l.Id, l.Code, l.Name))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
    }
}
