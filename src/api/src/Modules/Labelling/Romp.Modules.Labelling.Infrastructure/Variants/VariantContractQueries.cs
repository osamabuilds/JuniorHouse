using Microsoft.EntityFrameworkCore;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Infrastructure.Persistence;

namespace Romp.Modules.Labelling.Infrastructure.Variants;

/// <summary>The Labelling side of <see cref="IVariantQueries"/> (ADR 0014): each variant's current GTIN, or its last one when voided.</summary>
public sealed class VariantContractQueries(SkuDbContext dbContext) : IVariantQueries
{
    public async Task<IReadOnlyList<LabelledVariantInfo>> GetByStyleAsync(long styleId, CancellationToken cancellationToken)
    {
        var rows = await Project(dbContext.Variants.AsNoTracking().Where(v => v.StyleId == styleId).OrderBy(v => v.Id))
            .ToListAsync(cancellationToken);
        return rows.Select(Map).Where(v => v is not null).Select(v => v!).ToList();
    }

    public async Task<LabelledVariantInfo?> GetByIdAsync(long variantId, CancellationToken cancellationToken)
    {
        var row = await Project(dbContext.Variants.AsNoTracking().Where(v => v.Id == variantId)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Map(row);
    }

    private sealed record Row(long VariantId, long StyleId, short ColourId, short SizeId, string SkuCode, string? ActiveGtin, string? LastGtin);

    private static IQueryable<Row> Project(IQueryable<Domain.Variants.Variant> variants) =>
        variants.Select(v => new Row(
            v.Id,
            v.StyleId,
            v.ColourId,
            v.SizeId,
            v.SkuCode,
            v.Gtins.Where(g => g.StatusId == GtinStatus.Active).Select(g => g.Gtin).FirstOrDefault(),
            v.Gtins.OrderByDescending(g => g.Id).Select(g => g.Gtin).FirstOrDefault()));

    private static LabelledVariantInfo? Map(Row row) =>
        row.ActiveGtin is not null
            ? new LabelledVariantInfo(row.VariantId, row.StyleId, row.ColourId, row.SizeId, row.SkuCode, row.ActiveGtin, false)
            : row.LastGtin is not null
                ? new LabelledVariantInfo(row.VariantId, row.StyleId, row.ColourId, row.SizeId, row.SkuCode, row.LastGtin, true)
                : null;
}
