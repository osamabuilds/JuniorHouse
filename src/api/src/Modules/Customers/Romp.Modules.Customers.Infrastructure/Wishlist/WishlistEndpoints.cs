using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Wishlist;

namespace Romp.Modules.Customers.Infrastructure.Wishlist;

/// <summary>The body of a guest merge: product ids and nothing else (WSL-3).</summary>
public sealed record MergeWishlistRequest(IReadOnlyList<long> ProductIds);

/// <summary>
/// The caller's own wishlist. No route or body carries a customer id: every handler takes the customer from the signed-in principal and
/// filters by it in the same statement, so another customer's product is a plain 404 (WSL-7, SEC-05). Responses are never cached.
/// </summary>
internal static class WishlistEndpoints
{
    public static void MapWishlistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var list = endpoints.MapSecuredGroup("/api/customers/me/wishlist", Policies.Customer).WithTags("Customers").AddEndpointFilter<CustomerNoStoreFilter>();

        list.MapGet("", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListWishlistQuery(page, pageSize), cancellationToken)));

        list.MapGet("/ids", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetWishlistIdsQuery(), cancellationToken)));

        list.MapPut("/{productId:long}", async (long productId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new AddWishlistItemCommand(productId), cancellationToken);
            return Results.NoContent();
        });

        list.MapDelete("/{productId:long}", async (long productId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveWishlistItemCommand(productId), cancellationToken);
            return Results.NoContent();
        });

        list.MapPost("/merge", async (MergeWishlistRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(new { added = await sender.Send(new MergeGuestWishlistCommand(body.ProductIds ?? []), cancellationToken) }));
    }
}
