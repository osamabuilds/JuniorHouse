using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Production.Application.Milestones;
using Romp.Modules.Production.Application.ProductionRuns;

namespace Romp.Modules.Production.Infrastructure.Milestones;

public sealed record RecordMilestoneRequest(
    short TypeId,
    DateOnly ClaimedDate,
    string ReportedBy,
    string ActingPerson,
    string? Note,
    IReadOnlyList<QuantityLineDto>? FinishedLines);

internal static class MilestoneEndpoints
{
    public static void MapMilestoneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/production/runs").WithTags("Production");

        group.MapPost("/{id:long}/milestones", async (long id, RecordMilestoneRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new RecordMilestoneCommand(id, body.TypeId, body.ClaimedDate, body.ReportedBy, body.ActingPerson, body.Note, body.FinishedLines),
                cancellationToken)));
    }
}
