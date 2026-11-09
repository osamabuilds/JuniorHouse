using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Wishlist;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Application.Wishlist;

/// <summary>
/// Adds the products a guest tapped before signing in (WSL-3). The body is only a list of product ids from the caller's own browser; it adds
/// to the caller's own list and nothing else. Unknown or unpublished ids and ids already saved are skipped, so a repeat changes nothing, and
/// the list limit is never exceeded (the rest are dropped). Returns how many were added.
/// </summary>
public sealed record MergeGuestWishlistCommand(IReadOnlyList<long> ProductIds) : IRequest<int>, ICustomerCommand
{
    public const int MaxIds = 50;
}

public sealed class MergeGuestWishlistCommandValidator : AbstractValidator<MergeGuestWishlistCommand>
{
    public MergeGuestWishlistCommandValidator()
    {
        RuleFor(c => c.ProductIds).NotNull().Must(ids => ids is null || ids.Count <= MergeGuestWishlistCommand.MaxIds)
            .WithMessage($"At most {MergeGuestWishlistCommand.MaxIds} products can be merged at once.");
        RuleForEach(c => c.ProductIds).GreaterThan(0).WithMessage("Product ids must be positive.");
    }
}

public sealed class MergeGuestWishlistCommandHandler(
    ICustomerDbContext dbContext, ICurrentCustomer current, IStorefrontCatalogQueries catalog, IOptions<CustomersOptions> options)
    : IRequestHandler<MergeGuestWishlistCommand, int>
{
    public async Task<int> Handle(MergeGuestWishlistCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var wanted = request.ProductIds.Distinct().ToArray();
        if (wanted.Length == 0)
        {
            return 0;
        }

        var published = await catalog.GetProductSummariesAsync(wanted, cancellationToken);

        await dbContext.LockCustomerAsync(customerId, cancellationToken);
        var saved = (await dbContext.Wishlist.Where(w => w.CustomerId == customerId).Select(w => w.ProductId).ToListAsync(cancellationToken)).ToHashSet();

        var added = 0;
        foreach (var id in wanted)
        {
            var product = published.FirstOrDefault(p => p.ProductId == id);
            if (product is null || saved.Contains(id) || saved.Count >= options.Value.MaxWishlistItems)
            {
                continue;
            }

            dbContext.Wishlist.Add(WishlistItem.Create(customerId, id, product.Price));
            saved.Add(id);
            added++;
        }

        return added;
    }
}
