using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Labelling.Application.LabelWork;

namespace Romp.Modules.Labelling.Infrastructure.LabelWork;

/// <summary>Body of POST /api/labelling/work-items/{id}/resolve. The acting person is required on every write (NFR-SC-05).</summary>
public sealed record ResolveWorkItemRequest(string ActingPerson, bool ConfirmRetiredStyle = false);

/// <summary>Body of POST /api/labelling/work-items/{id}/print-jobs. The printer is a configured printer's code, never an address (PJ-12).</summary>
public sealed record QueueTagsRequest(int Qty, string ActingPerson, string? PrinterCode = null);

// Mapped through MapSecuredGroup (ADR 0011). No resource is owned by a user, so SEC-05 does not apply; roles arrive with Sprint 8.
internal static class LabelWorkEndpoints
{
    // A request body here is a couple of small fields; anything bigger is refused before it is read (ADR 0011).
    private const long MaxBodyBytes = 16 * 1024;

    public static void MapLabelWorkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/labelling/work-items").WithTags("Labelling")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/", async (
                string? status,
                long? styleId,
                bool? priceFlag,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchLabelWorkQuery(status, styleId, priceFlag, page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetLabelWorkItemQuery(id), cancellationToken)));

        group.MapPost("/{id:long}/resolve", async (long id, ResolveWorkItemRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ResolveWorkItemCommand(id, body.ConfirmRetiredStyle, body.ActingPerson), cancellationToken)))
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/{id:long}/print-jobs", async (long id, QueueTagsRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var job = await sender.Send(new QueueTagsCommand(id, body.Qty, body.PrinterCode, body.ActingPerson), cancellationToken);
                return Results.Created($"/api/labelling/print-jobs/{job.Id}", job);
            })
            .RequireRateLimiting(RateLimitPolicies.Strict);

        // PDF of what will be printed (TAG-8). Expensive-ish and read-only, so rate limited; served inline, never sniffed.
        group.MapGet("/{id:long}/tag-preview", async (long id, ISender sender, CancellationToken cancellationToken) =>
            {
                var preview = await sender.Send(new GetTagPreviewQuery(id), cancellationToken);
                return Results.File(preview.Content, "application/pdf", fileDownloadName: null, enableRangeProcessing: false);
            })
            .RequireRateLimiting(RateLimitPolicies.Strict)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");
    }
}
