using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Production.Application.ProductionRuns;

namespace Romp.Modules.Production.Infrastructure.ProductionRuns;

/// <summary>Body of PUT expected-completion. The acting person is required on every write (D13, AC-35).</summary>
public sealed record SetExpectedCompletionRequest(DateOnly Date, string? Reason, string ActingPerson);

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Every
// endpoint in this module is tagged "Production" for that sweep.
internal static class ProductionRunEndpoints
{
    public static void MapProductionRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/production/runs", Permissions.ProductionRead).WithTags("Production");

        group.MapGet("/", async (
                short? statusId,
                string? search,
                bool? atRisk,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchProductionRunsQuery(statusId, search, atRisk, page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetProductionRunQuery(id), cancellationToken)));

        group.MapGet("/by-po/{poId:long}", async (long poId, ISender sender, CancellationToken cancellationToken) =>
        {
            var run = await sender.Send(new GetProductionRunByPoQuery(poId), cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        group.MapPut("/{id:long}/expected-completion", async (long id, SetExpectedCompletionRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetExpectedCompletionCommand(id, body.Date, body.Reason, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.ProductionWrite);
    }
}
