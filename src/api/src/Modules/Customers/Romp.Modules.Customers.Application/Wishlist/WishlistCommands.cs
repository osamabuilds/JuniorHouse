using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Wishlist;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Application.Wishlist;

/// <summary>Saves a product to the caller's wishlist. Repeating it changes nothing (WSL-4). The price saved comes from the read model, never the client (WSL-6).</summary>
public sealed record AddWishlistItemCommand(long ProductId) : IRequest, ICustomerCommand;

/// <summary>Removes a product from the caller's own wishlist. A product that is not on it, or is on someone else's, is a 404 and changes nothing (WSL-7).</summary>
public sealed record RemoveWishlistItemCommand(long ProductId) : IRequest, ICustomerCommand;

/// <summary>
/// Locks the customer's row first, so the 200 limit and the one-row-per-product rule hold when two requests arrive together; the unique index
/// is the backstop (WSL-4). A product the storefront no longer publishes cannot be saved.
/// </summary>
public sealed class AddWishlistItemCommandHandler(
    ICustomerDbContext dbContext, ICurrentCustomer current, IStorefrontCatalogQueries catalog, IOptions<CustomersOptions> options)
    : IRequestHandler<AddWishlistItemCommand>
{
    public async Task Handle(AddWishlistItemCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var products = await catalog.GetProductSummariesAsync([request.ProductId], cancellationToken);
        var product = products.Count > 0 ? products[0] : null;
        if (product is null)
        {
            throw new KeyNotFoundException("Product not found.");
        }

        await dbContext.LockCustomerAsync(customerId, cancellationToken);
        var saved = await dbContext.Wishlist.Where(w => w.CustomerId == customerId).Select(w => w.ProductId).ToListAsync(cancellationToken);
        var already = saved.Contains(product.ProductId);
        WishlistList.EnsureRoom(saved.Count, already, options.Value.MaxWishlistItems);
        if (!already)
        {
            dbContext.Wishlist.Add(WishlistItem.Create(customerId, product.ProductId, product.Price));
        }
    }
}

public sealed class RemoveWishlistItemCommandHandler(ICustomerDbContext dbContext, ICurrentCustomer current) : IRequestHandler<RemoveWishlistItemCommand>
{
    public async Task Handle(RemoveWishlistItemCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var item = await dbContext.Wishlist.FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException("Product is not on your wishlist.");

        dbContext.Wishlist.Remove(item);
    }
}
