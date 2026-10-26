using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Quality.Application.Outcomes;

namespace Romp.Modules.Quality.Infrastructure.Outcomes;

/// <summary>Body of POST /api/quality/batches/{id}/decision: one part for the whole batch, or several parts that add up to it (AC-20, AC-22).</summary>
public sealed record DecideBatchRequest(string ActingPerson, IReadOnlyList<DecisionPartDto> Parts);

/// <summary>Body of POST /api/quality/sub-batches/{id}/decision: the outcome for a returned rework part (AC-26).</summary>
public sealed record DecideSubBatchRequest(short OutcomeId, string? Reason, string ActingPerson);

/// <summary>Body of POST /api/quality/sub-batches/{id}/rework-return (AC-26, AC-27).</summary>
public sealed record RecordReworkReturnRequest(DateOnly ReturnDate, string ActingPerson, string? Note, IReadOnlyList<PartLineDto> Lines);

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Tagged "Quality" for that sweep.
internal static class OutcomeEndpoints
{
    public static void MapOutcomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/quality", Permissions.QcRead).WithTags("Quality");

        group.MapPost("/batches/{id:long}/decision", async (long id, DecideBatchRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecideBatchCommand(id, body.ActingPerson, body.Parts), cancellationToken))).RequirePermission(Permissions.QcWrite);

        group.MapPost("/sub-batches/{id:long}/decision", async (long id, DecideSubBatchRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecideSubBatchCommand(id, body.OutcomeId, body.Reason, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.QcWrite);

        group.MapPost("/sub-batches/{id:long}/rework-return", async (long id, RecordReworkReturnRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RecordReworkReturnCommand(id, body.ReturnDate, body.ActingPerson, body.Note, body.Lines), cancellationToken))).RequirePermission(Permissions.QcWrite);
    }
}
