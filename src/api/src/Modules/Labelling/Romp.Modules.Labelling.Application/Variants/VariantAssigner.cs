using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Application.Variants;

/// <summary>
/// Implements <see cref="IVariantAssigner"/> (VAR-1..VAR-9, ADR 0009). For each requested variant: reuse it if it already has an
/// active GTIN; otherwise create it if needed and take the next item reference from the atomic counter. Everything happens in the
/// caller's transaction, so a rollback releases the number again and nothing is ever issued twice. A concurrent creation of the
/// same variant fails on the unique index and the whole message is retried (the retry finds the winner).
/// </summary>
public sealed class VariantAssigner(ILabellingDbContext dbContext, IGtinSequence sequence, IOptions<LabellingOptions> options)
    : IVariantAssigner
{
    public async Task<IReadOnlyDictionary<(long StyleId, short ColourId, short SizeId), long>> EnsureGtinsAsync(
        IReadOnlyCollection<VariantRequest> requests, CancellationToken cancellationToken)
    {
        var prefix = options.Value.Prefix;
        var result = new Dictionary<(long, short, short), long>();

        foreach (var request in requests.DistinctBy(r => (r.StyleId, r.ColourId, r.SizeId)))
        {
            var variant = await dbContext.Variants.Include(v => v.Gtins)
                .FirstOrDefaultAsync(
                    v => v.StyleId == request.StyleId && v.ColourId == request.ColourId && v.SizeId == request.SizeId,
                    cancellationToken);

            if (variant is null)
            {
                variant = new Variant(
                    request.StyleId, request.ColourId, request.SizeId,
                    SkuCodeBuilder.Build(request.StyleCode, request.ColourCode, request.SizeCode));
                dbContext.Variants.Add(variant);
                await dbContext.SaveChangesAsync(cancellationToken); // the events carry the variant id, so it needs one first
            }

            if (variant.ActiveGtin is null)
            {
                var reference = await sequence.NextAsync(prefix.Value, cancellationToken);
                if (reference >= prefix.Capacity)
                {
                    throw new DomainException(
                        $"The GTIN range for prefix {prefix.Value} is used up ({prefix.Capacity:N0} numbers). Ask GS1 for a new prefix before labelling more stock.");
                }

                variant.AssignGtin(prefix, reference);
            }

            result[(request.StyleId, request.ColourId, request.SizeId)] = variant.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}

/// <summary>Implements <see cref="IVariantReader"/> over the module's own tables.</summary>
public sealed class VariantReader(ILabellingDbContext dbContext) : IVariantReader
{
    public async Task<VariantSnapshot?> GetByVariantIdAsync(long variantId, CancellationToken cancellationToken) =>
        await dbContext.Variants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new VariantSnapshot(
                v.Id, v.StyleId, v.ColourId, v.SizeId, v.SkuCode,
                v.Gtins.Where(g => g.StatusId == GtinStatus.Active).Select(g => (long?)g.Id).FirstOrDefault(),
                v.Gtins.Where(g => g.StatusId == GtinStatus.Active).Select(g => g.Gtin).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<VariantSnapshot?> GetByGtinIdAsync(long gtinId, CancellationToken cancellationToken) =>
        await (from g in dbContext.Gtins.AsNoTracking()
               join v in dbContext.Variants.AsNoTracking() on g.VariantId equals v.Id
               where g.Id == gtinId
               select new VariantSnapshot(
                   v.Id, v.StyleId, v.ColourId, v.SizeId, v.SkuCode,
                   g.StatusId == GtinStatus.Active ? g.Id : null,
                   g.StatusId == GtinStatus.Active ? g.Gtin : null))
            .FirstOrDefaultAsync(cancellationToken);
}
