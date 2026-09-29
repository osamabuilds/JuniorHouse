using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.VendorResponses;

namespace Romp.Modules.Vendor.Infrastructure.VendorResponses;

internal static class VendorResponseEndpoints
{
    public static void MapVendorResponseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Vendor");

        // SCRUM-93 task 33. The route's id always wins over any PoId in the body.
        group.MapPost("/{id:long}/vendor-response", async (
                long id,
                RecordVendorResponseCommand body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body with { PoId = id }, cancellationToken)));

        group.MapPost("/{id:long}/vendor-amendment-request", async (
                long id,
                RecordVendorAmendmentRequestCommand body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body with { PoId = id }, cancellationToken)));
    }
}
