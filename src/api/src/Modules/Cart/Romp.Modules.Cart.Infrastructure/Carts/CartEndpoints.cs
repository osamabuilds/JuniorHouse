using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.Totals;
using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Infrastructure.Carts;

/// <summary>Add a variant. Only these two fields exist: a client-sent price or total has nowhere to land (CRT-6, SEC-09).</summary>
public sealed record AddLineRequest(long VariantId, int Quantity);

/// <summary>Set a line's quantity.</summary>
public sealed record SetQuantityRequest(int Quantity);

/// <summary>
/// The shopper's basket (cart plan, "API endpoints"). Open to guests by design: the owner is the signed-in customer or the guest cookie,
/// read by <see cref="CartOwnerAccessor"/> and never taken from a route or body, so another owner's basket cannot even be named (CRT-9).
/// Writes also check the browser's <c>Origin</c>, and the body is capped at 2 KB.
/// </summary>
internal static class CartEndpoints
{
    private const long MaxBodyBytes = 2 * 1024;

    public static void MapCartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cart = endpoints.MapSecuredGroup("/api/cart", Policies.Public)
            .WithTags("Cart")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CartNoStoreFilter>();

        cart.MapGet("", async (CartOwnerAccessor owners, ISender sender, CancellationToken cancellationToken, int? cityId = null, string? method = null) =>
        {
            // A read never creates a basket or a cookie: a guest with none sees an empty one.
            var owner = await owners.ReadAsync(cancellationToken);
            var result = await sender.Send(new GetCartWithTotalsQuery(owner, cityId, string.Equals(method, "cod", StringComparison.OrdinalIgnoreCase)), cancellationToken);
            return Results.Ok(CartResponse.From(result.Cart, result.Amounts));
        });

        cart.MapPost("/lines", async (AddLineRequest body, CartOwnerAccessor owners, ISender sender, IServiceScopeFactory scopes, CancellationToken cancellationToken) =>
        {
            var owner = await owners.ReadOrCreateAsync(cancellationToken);
            var request = new AddLineCommand(owner, body.VariantId, body.Quantity);
            return Results.Ok(CartResponse.From(await SendOnceMoreOnRace(sender, scopes, request, cancellationToken)));
        }).AddEndpointFilter<CartOriginCheckFilter>();

        cart.MapPut("/lines/{variantId:long}", async (long variantId, SetQuantityRequest body, CartOwnerAccessor owners, ISender sender, CancellationToken cancellationToken) =>
        {
            var owner = await owners.ReadOrCreateAsync(cancellationToken);
            return Results.Ok(CartResponse.From(await sender.Send(new SetQuantityCommand(owner, variantId, body.Quantity), cancellationToken)));
        }).AddEndpointFilter<CartOriginCheckFilter>();

        cart.MapDelete("/lines/{variantId:long}", async (long variantId, CartOwnerAccessor owners, ISender sender, CancellationToken cancellationToken) =>
        {
            var owner = await owners.ReadOrCreateAsync(cancellationToken);
            return Results.Ok(CartResponse.From(await sender.Send(new RemoveLineCommand(owner, variantId), cancellationToken)));
        }).AddEndpointFilter<CartOriginCheckFilter>();
    }

    /// <summary>
    /// Two first adds from one browser (a double click on a new guest) can both try to create the basket; the unique index lets one win and
    /// the other, on a fresh scope with a clean change tracker, simply adds to the basket that now exists.
    /// </summary>
    private static async Task<PricedCart> SendOnceMoreOnRace(ISender sender, IServiceScopeFactory scopes, AddLineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(request, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, cancellationToken);
        }
    }
}
