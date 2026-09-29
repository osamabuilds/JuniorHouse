using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.Inspections.Aql;

namespace Romp.Modules.Quality.Infrastructure.Batches;

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Tagged "Quality" for that sweep.
internal static class QcBatchEndpoints
{
    public static void MapQcBatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/quality").WithTags("Quality");

        group.MapGet("/batches", async (
                short? statusId,
                string? search,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchQcBatchesQuery(statusId, search, page, pageSize), cancellationToken)));

        group.MapGet("/batches/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetQcBatchQuery(id), cancellationToken)));

        // The calculator preview: level codes are the stable lookup codes (I, II, III; Aql2_5 ...), defaults come from Qc:* configuration.
        group.MapGet("/aql/plan", async (
                int lotQty,
                string? inspectionLevel,
                string? aqlLevel,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new PreviewAqlPlanQuery(
                    lotQty,
                    inspectionLevel is null ? null : PlanCodes.InspectionLevelId(inspectionLevel),
                    aqlLevel is null ? null : PlanCodes.AqlLevelId(aqlLevel)),
                cancellationToken)));
    }
}
