using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.Inspections;

namespace Romp.Modules.Quality.Infrastructure.Inspections;

/// <summary>Body of POST /api/quality/batches/{id}/inspections. The acting person is required on every write (AC-30).</summary>
public sealed record RecordInspectionRequest(
    DateOnly InspectionDate,
    string ActingPerson,
    int? SampleQty,
    short? InspectionLevelId,
    short? AqlLevelId,
    long? SubBatchId,
    IReadOnlyList<CheckResultDto> Checks,
    IReadOnlyList<DefectDto> Defects);

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Tagged "Quality" for that sweep.
internal static class InspectionEndpoints
{
    public static void MapInspectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/quality/batches").WithTags("Quality");

        group.MapPost("/{id:long}/inspections", async (long id, RecordInspectionRequest body, ISender sender, CancellationToken cancellationToken) =>
        {
            var inspection = await sender.Send(
                new RecordInspectionCommand(
                    id, body.SubBatchId, body.InspectionDate, body.ActingPerson, body.SampleQty, body.InspectionLevelId, body.AqlLevelId,
                    body.Checks, body.Defects),
                cancellationToken);
            return Results.Created($"/api/quality/batches/{id}", inspection);
        });
    }
}
