using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Infrastructure.Persistence;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

/// <summary>The CTLG side of the VNDR-facing contract (task SCRUM-92, AC-7/AC-8).</summary>
public sealed class StyleQueries(CatalogDbContext dbContext) : IStyleQueries
{
    public async Task<StyleSummary?> FindActiveStyleAsync(long styleId, CancellationToken cancellationToken)
    {
        var style = await dbContext.Styles
            .AsNoTracking()
            .Include(s => s.Colourways)
            .Include(s => s.Sizes)
            .FirstOrDefaultAsync(s => s.Id == styleId && s.IsActive, cancellationToken);

        return style is null
            ? null
            : new StyleSummary(
                style.Id,
                style.Code,
                style.Name,
                style.Sizes.Select(s => s.SizeId).ToList(),
                style.Colourways.Select(c => c.ColourId).ToList());
    }

    public async Task<IReadOnlyDictionary<long, StyleLabelInfo>> GetLabelInfoAsync(
        IReadOnlyCollection<long> styleIds, CancellationToken cancellationToken)
    {
        if (styleIds.Count == 0)
        {
            return new Dictionary<long, StyleLabelInfo>();
        }

        var ids = styleIds.Distinct().ToList();
        return await dbContext.Styles
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new StyleLabelInfo(
                s.Id, s.Code, s.Name, s.TargetRetailPrice, s.TargetPriceChangedAt ?? s.InsrDte, s.IsActive))
            .ToDictionaryAsync(info => info.Id, cancellationToken);
    }
}
