using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal static class PurchaseOrderEndpoints
{
    public static void MapPurchaseOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Vendor");

        group.MapPost("/", async (CreatePurchaseOrderCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken)));

        group.MapPut("/{id:long}", async (
                long id,
                UpdatePoRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/send", async (long id, bool? sendWithoutTechPack, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SendPurchaseOrderCommand(id, sendWithoutTechPack ?? false), cancellationToken)));

        group.MapPost("/{id:long}/acknowledge", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AcknowledgePurchaseOrderCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/cancel", async (
                long id,
                CancelPoRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CancelPurchaseOrderCommand(id, body.CancelReasonId), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var po = await sender.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken);
            return po is null ? Results.NotFound() : Results.Ok(po);
        });

        group.MapGet("/", async (
                long? vendorId,
                short? statusId,
                DateOnly? deliveryFrom,
                DateOnly? deliveryTo,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new SearchPurchaseOrdersQuery(vendorId, statusId, deliveryFrom, deliveryTo),
                cancellationToken)));
    }
}
