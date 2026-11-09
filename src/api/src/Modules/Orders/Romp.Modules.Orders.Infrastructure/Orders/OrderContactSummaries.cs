using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// Masked order contacts for the Notifications delivery log (DLG-6, SEC-25). One query per call: a page of the log never costs a query per
/// row. Search is exact on the order number, the email or the normalised mobile, so the log cannot be used to browse personal data.
/// </summary>
internal sealed class OrderContactSummaries(OrdrDbContext dbContext) : IOrderContactSummaries
{
    public async Task<IReadOnlyDictionary<long, OrderContactSummary>> FindManyAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<long, OrderContactSummary>();
        }

        var ids = orderIds.Distinct().ToArray();
        var rows = await dbContext.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNumber, o.ContactEmail, o.ContactMobile })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.Id,
            r => new OrderContactSummary(r.Id, r.OrderNumber, ContactMasking.MaskEmail(r.ContactEmail), ContactMasking.MaskMobile(r.ContactMobile)));
    }

    public async Task<IReadOnlyList<long>> FindOrderIdsAsync(string term, int max, CancellationToken cancellationToken)
    {
        var text = (term ?? string.Empty).Trim();
        if (text.Length == 0 || max <= 0)
        {
            return [];
        }

        var upper = text.ToUpperInvariant();
        // ILIKE with the wildcards escaped is an exact, case-insensitive match: addresses are stored as typed.
        var emailPattern = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        var mobile = PhoneNumber.TryNormalise(text, out var normalised) ? normalised : null;

        return await dbContext.Orders.AsNoTracking()
            .Where(o => o.OrderNumber == upper
                || (o.ContactEmail != null && EF.Functions.ILike(o.ContactEmail, emailPattern))
                || (mobile != null && o.ContactMobile == mobile))
            .OrderByDescending(o => o.Id)
            .Select(o => o.Id)
            .Take(max)
            .ToListAsync(cancellationToken);
    }
}
