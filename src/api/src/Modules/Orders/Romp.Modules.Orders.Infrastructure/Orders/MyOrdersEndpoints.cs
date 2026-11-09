using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Infrastructure.Checkout;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// The signed-in customer's own orders (orders P8, ORD-13, ORD-14). The customer policy runs on every request; no route or body carries a
/// customer id, and the handlers read the rows for the session's customer only, so another customer's order is a plain 404 (SEC-05).
/// Responses are never cached.
/// </summary>
internal static class MyOrdersEndpoints
{
    public static void MapMyOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var mine = endpoints.MapSecuredGroup("/api/orders/mine", Policies.Customer).WithTags("Orders").AddEndpointFilter<OrdersNoStoreFilter>();

        mine.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListMyOrdersQuery(page, pageSize), cancellationToken)));

        mine.MapGet("/{orderNumber}", async (string orderNumber, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetMyOrderQuery(orderNumber), cancellationToken) is { } found ? Results.Ok(found) : Results.NotFound());
    }
}
