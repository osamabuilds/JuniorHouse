using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;

namespace Romp.Modules.Catalog.Infrastructure;

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
}
