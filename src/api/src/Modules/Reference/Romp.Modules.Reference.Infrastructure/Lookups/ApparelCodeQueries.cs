using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Infrastructure.Persistence;

namespace Romp.Modules.Reference.Infrastructure.Lookups;

/// <summary>The REF side of the labelling contract (Sprint 5): colour and size codes and names in one query each.</summary>
public sealed class ApparelCodeQueries(ReferenceDbContext dbContext) : IApparelCodeQueries
{
    public async Task<IReadOnlyDictionary<short, ApparelCode>> GetColoursAsync(
        IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<short, ApparelCode>();
        }

        var wanted = ids.Distinct().ToList();
        return await dbContext.Set<ColourLookup>().AsNoTracking()
            .Where(l => wanted.Contains(l.Id))
            .Select(l => new ApparelCode(l.Id, l.Code, l.Name))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<short, ApparelCode>> GetSizesAsync(
        IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<short, ApparelCode>();
        }

        var wanted = ids.Distinct().ToList();
        return await dbContext.Set<SizeLookup>().AsNoTracking()
            .Where(l => wanted.Contains(l.Id))
            .Select(l => new ApparelCode(l.Id, l.Code, l.Name))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
    }
}
