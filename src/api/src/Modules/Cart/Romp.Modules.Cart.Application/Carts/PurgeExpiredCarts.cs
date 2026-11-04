using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Application.Abstractions;

namespace Romp.Modules.Cart.Application.Carts;

/// <summary>Deletes guest baskets that have passed their lifetime, with their lines (CRT-10). Account baskets never expire. Returns how many were removed.</summary>
public sealed record PurgeExpiredCartsCommand : IRequest<int>, ICartCommand;

public sealed class PurgeExpiredCartsCommandHandler(ICartDbContext dbContext, TimeProvider clock) : IRequestHandler<PurgeExpiredCartsCommand, int>
{
    /// <summary>Removed per round, so a large backlog never holds one long lock.</summary>
    public const int BatchSize = 500;

    public async Task<int> Handle(PurgeExpiredCartsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var total = 0;
        int removed;
        do
        {
            var ids = await dbContext.Carts.AsNoTracking()
                .Where(c => c.ExpiresAt != null && c.ExpiresAt < now)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            removed = ids.Count == 0 ? 0 : await dbContext.Carts.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
            total += removed;
        }
        while (removed == BatchSize);

        return total;
    }
}
