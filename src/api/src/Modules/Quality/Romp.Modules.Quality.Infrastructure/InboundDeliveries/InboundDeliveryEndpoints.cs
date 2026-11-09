using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Quality.Application.InboundDeliveries;

namespace Romp.Modules.Quality.Infrastructure.InboundDeliveries;

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Every endpoint in this
// module is tagged "Quality" for that sweep.
internal static class InboundDeliveryEndpoints
{
    public static void MapInboundDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/quality/inbound-deliveries", Permissions.QcRead).WithTags("Quality");

        group.MapGet("/", async (
                string? status,
                string? search,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchInboundDeliveriesQuery(status, search, page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetInboundDeliveryQuery(id), cancellationToken)));
    }
}
