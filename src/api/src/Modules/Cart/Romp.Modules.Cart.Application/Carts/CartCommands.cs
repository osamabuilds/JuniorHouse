using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Cart.Application.Carts;

/// <summary>
/// Add a variant to the caller's basket (creating the basket on first use). Only the variant id and the quantity are read: any price or total
/// a client sends has nowhere to go (CRT-6, SEC-09).
/// </summary>
public sealed record AddLineCommand(CartOwner Owner, long VariantId, int Quantity) : IRequest<PricedCart>, ICartCommand;

/// <summary>Set an existing line to a new quantity (CRT-1). Zero is refused; removing is a separate act (CRT-11).</summary>
public sealed record SetQuantityCommand(CartOwner Owner, long VariantId, int Quantity) : IRequest<PricedCart>, ICartCommand;

/// <summary>Remove a line (CRT-2). Removing something that is not there is not an error.</summary>
public sealed record RemoveLineCommand(CartOwner Owner, long VariantId) : IRequest<PricedCart>, ICartCommand;

/// <summary>What the three commands share: finding the basket, the live stock and sellable checks (CRT-4, CRT-5), and pricing the answer.</summary>
public sealed class CartCommandSupport(
    ICartDbContext dbContext, IVariantSalesQueries catalog, IStockAvailabilityQueries stock, CartPricer pricer, IOptions<CartOptions> options, TimeProvider clock)
{
    public CartOptions Options => options.Value;

    public DateTimeOffset Now => clock.GetUtcNow();

    public async Task<ShoppingCart> FindOrCreateAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await CartStore.FindAsync(dbContext.Carts, owner, cancellationToken);
        if (cart is not null && !cart.IsExpired(Now))
        {
            return cart;
        }

        if (cart is not null)
        {
            // An expired guest basket starts again empty (CRT-10); its row is reused so the one-basket-per-owner rule never trips.
            cart.Clear(Now, Options.GuestLifetime);
            return cart;
        }

        cart = ShoppingCart.Create(owner, Now, Options.GuestLifetime);
        dbContext.Carts.Add(cart);
        return cart;
    }

    /// <summary>The variant as the shopper may buy it, or a refusal that says so (CRT-5). Absent, unpublished, unlabelled and unpriced all read the same.</summary>
    public async Task<VariantSale> RequireSellableAsync(long variantId, CancellationToken cancellationToken)
    {
        var found = await catalog.GetManyAsync([variantId], cancellationToken);
        var sale = found.Count > 0 ? found[0] : null;
        return sale is { IsSellable: true, Price: > 0 }
            ? sale
            : throw Refuse("variantId", "This item is not available right now.");
    }

    /// <summary>Refuses a quantity above what the warehouse has, naming the count (CRT-4).</summary>
    public async Task RequireStockAsync(VariantSale sale, int quantity, CancellationToken cancellationToken)
    {
        var available = 0;
        if (sale.LabellingVariantId is { } labellingId)
        {
            var rows = await stock.GetManyAsync([labellingId], cancellationToken);
            available = rows.Count > 0 ? rows[0].Available : 0;
        }

        if (quantity > available)
        {
            throw Refuse(
                "quantity",
                available <= 0 ? "That item is out of stock." : $"Only {available} {(available == 1 ? "is" : "are")} in stock.");
        }
    }

    public async Task<PricedCart> PriceAsync(ShoppingCart cart, CancellationToken cancellationToken)
    {
        var priced = await pricer.PriceAsync(cart, cancellationToken);
        cart.NotePrices(priced.Lines.Where(l => l.UnitPrice is not null).ToDictionary(l => l.VariantId, l => l.UnitPrice!.Value));
        return priced;
    }

    private static ValidationException Refuse(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class AddLineCommandHandler(CartCommandSupport support) : IRequestHandler<AddLineCommand, PricedCart>
{
    public async Task<PricedCart> Handle(AddLineCommand request, CancellationToken cancellationToken)
    {
        var sale = await support.RequireSellableAsync(request.VariantId, cancellationToken);
        var cart = await support.FindOrCreateAsync(request.Owner, cancellationToken);
        var held = cart.Lines.FirstOrDefault(l => l.VariantId == request.VariantId)?.Quantity ?? 0;

        // The domain checks 1..maximum and the line limit; the stock check needs the total the line would hold (CRT-4).
        var options = support.Options;
        if (request.Quantity >= 1 && held + request.Quantity <= options.MaxPerLine)
        {
            await support.RequireStockAsync(sale, held + request.Quantity, cancellationToken);
        }

        cart.AddLine(request.VariantId, request.Quantity, options.MaxPerLine, options.MaxLines, support.Now, options.GuestLifetime);
        return await support.PriceAsync(cart, cancellationToken);
    }
}

public sealed class SetQuantityCommandHandler(CartCommandSupport support) : IRequestHandler<SetQuantityCommand, PricedCart>
{
    public async Task<PricedCart> Handle(SetQuantityCommand request, CancellationToken cancellationToken)
    {
        var options = support.Options;
        var cart = await support.FindOrCreateAsync(request.Owner, cancellationToken);
        if (request.Quantity >= 1 && request.Quantity <= options.MaxPerLine && cart.Lines.Any(l => l.VariantId == request.VariantId))
        {
            var sale = await support.RequireSellableAsync(request.VariantId, cancellationToken);
            await support.RequireStockAsync(sale, request.Quantity, cancellationToken);
        }

        cart.SetQuantity(request.VariantId, request.Quantity, options.MaxPerLine, support.Now, options.GuestLifetime);
        return await support.PriceAsync(cart, cancellationToken);
    }
}

public sealed class RemoveLineCommandHandler(CartCommandSupport support) : IRequestHandler<RemoveLineCommand, PricedCart>
{
    public async Task<PricedCart> Handle(RemoveLineCommand request, CancellationToken cancellationToken)
    {
        var cart = await support.FindOrCreateAsync(request.Owner, cancellationToken);
        cart.RemoveLine(request.VariantId, support.Now, support.Options.GuestLifetime);
        return await support.PriceAsync(cart, cancellationToken);
    }
}
