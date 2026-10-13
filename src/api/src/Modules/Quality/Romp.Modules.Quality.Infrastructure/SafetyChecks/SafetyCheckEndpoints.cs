using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.SafetyChecks;

namespace Romp.Modules.Quality.Infrastructure.SafetyChecks;

/// <summary>Body of PUT /api/quality/inspections/{id}/safety-checks: both checks in one call, with the acting person (AC-30, AC-31).</summary>
public sealed record RecordSafetyChecksRequest(string ActingPerson, IReadOnlyList<SafetyCheckDto> Checks);

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Tagged "Quality" for that sweep.
internal static class SafetyCheckEndpoints
{
    public static void MapSafetyCheckEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/quality/inspections").WithTags("Quality");

        group.MapPut("/{id:long}/safety-checks", async (long id, RecordSafetyChecksRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RecordSafetyChecksCommand(id, body.ActingPerson, body.Checks), cancellationToken)));
    }
}
