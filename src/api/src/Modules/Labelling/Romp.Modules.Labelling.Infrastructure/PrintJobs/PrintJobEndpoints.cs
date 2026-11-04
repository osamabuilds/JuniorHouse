using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Labelling.Application.PrintJobs;

namespace Romp.Modules.Labelling.Infrastructure.PrintJobs;

/// <summary>Body of POST /api/labelling/variants/{id}/reprints (FR-SC-07). A reason and the acting person are required.</summary>
public sealed record ReprintTagRequest(int Qty, short ReasonId, string ActingPerson, string? Note = null, string? PrinterCode = null);

/// <summary>Body of the retry and cancel endpoints.</summary>
public sealed record PrintJobActionRequest(string ActingPerson);

// Mapped through MapSecuredGroup (ADR 0011). Nothing here accepts a printer address; printers are chosen by configured code only (PJ-12).
internal static class PrintJobEndpoints
{
    private const long MaxBodyBytes = 16 * 1024;

    public static void MapPrintJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/labelling", Permissions.LabellingRead).WithTags("Labelling")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/print-jobs", async (
                string? status,
                short? printerId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchPrintJobsQuery(status, printerId, page, pageSize), cancellationToken)));

        group.MapPost("/print-jobs/{id:long}/retry", async (long id, PrintJobActionRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new RetryPrintJobCommand(id, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.LabellingWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        // Cancel is an operational action: ops.admin (roles spec).
        group.MapPost("/print-jobs/{id:long}/cancel", async (long id, PrintJobActionRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new CancelPrintJobCommand(id, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.OpsAdmin)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/variants/{id:long}/reprints", async (long id, ReprintTagRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var job = await sender.Send(
                    new ReprintTagCommand(id, body.Qty, body.ReasonId, body.Note, body.PrinterCode, body.ActingPerson), cancellationToken);
                return Results.Created($"/api/labelling/print-jobs/{job.Id}", job);
            }).RequirePermission(Permissions.LabellingWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapGet("/variants/{id:long}/print-history", async (
                long id,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new GetPrintHistoryQuery(id, page, pageSize), cancellationToken)));

        group.MapGet("/printers", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListPrintersQuery(), cancellationToken)));
    }
}
