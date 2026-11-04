using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Infrastructure.Newsletter;

/// <summary>The Customers side of <see cref="INewsletterAudience"/>: one keyset query over subscribed records, the unique id order, no joins.</summary>
public sealed class NewsletterAudience(CustDbContext dbContext) : INewsletterAudience
{
    public async Task<IReadOnlyList<SubscribedAddress>> ListSubscribedAsync(long afterId, int take, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(take, 1, INewsletterAudience.MaxTake);
        return await dbContext.Newsletter.AsNoTracking()
            .Where(s => s.Id > afterId && s.Status == NewsletterStatus.Subscribed)
            .OrderBy(s => s.Id)
            .Take(size)
            .Select(s => new SubscribedAddress(s.Id, s.Email))
            .ToListAsync(cancellationToken);
    }
}
