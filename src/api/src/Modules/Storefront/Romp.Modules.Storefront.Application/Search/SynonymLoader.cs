using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.Search;

/// <summary>Reads the active synonym groups in one query (search and autosuggest share it behind <see cref="ISynonymCache"/>).</summary>
internal static class SynonymLoader
{
    public static async Task<SynonymExpansion> LoadAsync(IStorefrontReadDbContext db, CancellationToken cancellationToken)
    {
        var terms = await (
            from t in db.SynonymTerms.AsNoTracking()
            join g in db.SynonymGroups.AsNoTracking() on t.GroupId equals g.Id
            where g.IsActive
            select new { t.GroupId, t.Term }).ToListAsync(cancellationToken);

        return new SynonymExpansion(terms.GroupBy(t => t.GroupId).Select(g => (IReadOnlyCollection<string>)g.Select(t => t.Term).ToList()));
    }
}
