using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Infrastructure;

/// <summary>The REF side of the VNDR-facing contract (AC-7a).</summary>
public sealed class PaymentTermQueries(ReferenceDbContext dbContext) : IPaymentTermQueries
{
    public async Task<decimal?> GetDefaultAdvancePercentAsync(short paymentTermId, CancellationToken cancellationToken) =>
        await dbContext.Set<PaymentTermLookup>()
            .AsNoTracking()
            .Where(term => term.Id == paymentTermId)
            .Select(term => (decimal?)term.DefaultAdvancePercent)
            .FirstOrDefaultAsync(cancellationToken);
}
