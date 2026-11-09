using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.Labels;
using Romp.Modules.Shipping.Application.ManualBookingQueue;
using Romp.Modules.Shipping.Application.Tracking;

namespace Romp.Modules.Shipping.Infrastructure.Bookings;

/// <summary>Body of POST /shipments/{id}/manual-waybill: the courier and the waybill staff got by phone (MBQ-4).</summary>
public sealed record ManualWaybillRequest(string? Courier, string? Waybill);

/// <summary>
/// The staff routes of shipping (bookings, manual booking queue, labels, shipments). Reading needs <c>shipping.view</c>; every change needs
/// <c>shipping.ops</c>, both checked on every request (SEC-08). The staff member comes from the session, never from the body (NFR-SC-05). The label
/// holds the customer's phone and address, so it is for staff only and is never cached (LBL-7).
/// </summary>
internal static class BookingEndpoints
{
    private const long MaxBodyBytes = 1024;

    public static void MapBookingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/shipping", Permissions.ShippingView).WithTags("Shipping")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/shipments", async (ISender sender, CancellationToken cancellationToken, string? status = null, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListShipmentsQuery(status, page, pageSize), cancellationToken)));

        group.MapGet("/shipments/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetShipmentQuery(id), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound());

        group.MapGet("/queue", async (ISender sender, CancellationToken cancellationToken, bool needsStaffOnly = false, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListManualQueueQuery(needsStaffOnly, page, pageSize), cancellationToken)));

        group.MapGet("/shipments/{id:long}/label", async (long id, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await sender.Send(new GetLabelQuery(id), cancellationToken) is { } label ? Results.Ok(label) : Results.NotFound();
        });

        group.MapPost("/shipments/{id:long}/retry", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            if (!await sender.Send(new RetryNowCommand(id, actor.UserName), cancellationToken))
            {
                return Results.NotFound();
            }

            // The booking is its own step: a repeat press while one runs finds the shipment already claimed and does nothing more (MBQ-3).
            var outcome = await sender.Send(new BookShipmentCommand(id), cancellationToken);
            return Results.Ok(new { outcome = outcome.ToString() });
        }).RequirePermission(Permissions.ShippingOps);

        group.MapPost("/shipments/{id:long}/manual-waybill", async (long id, ManualWaybillRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new EnterManualWaybillCommand(id, body.Courier ?? string.Empty, body.Waybill ?? string.Empty, actor.UserName), cancellationToken)
                ? Results.Ok(await sender.Send(new GetShipmentQuery(id), cancellationToken))
                : Results.NotFound()).RequirePermission(Permissions.ShippingOps);

        group.MapPost("/shipments/{id:long}/label/printed", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new MarkLabelPrintedCommand(id), cancellationToken) ? Results.NoContent() : Results.NotFound()).RequirePermission(Permissions.ShippingOps);
    }
}
