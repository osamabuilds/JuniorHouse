using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Labelling.Application.Variants;

namespace Romp.Modules.Labelling.Infrastructure.Variants;

/// <summary>Body of POST /api/labelling/variants/{id}/void. A reason code and the acting person are required (NFR-SC-05).</summary>
public sealed record VoidGtinRequest(short ReasonId, string ActingPerson, string? Note = null);

// Mapped through MapSecuredGroup (ADR 0011). Assignment has no endpoint at all: GTINs are only ever given out by label-work (D9).
internal static class VariantEndpoints
{
    private const long MaxBodyBytes = 16 * 1024;

    public static void MapVariantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/labelling").WithTags("Labelling")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/variants", async (
                string? search,
                string? status,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchVariantsQuery(search, status, page, pageSize), cancellationToken)));

        group.MapGet("/variants/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetVariantQuery(id), cancellationToken)));

        // Void is an Admin action: declared now, enforced with roles in Sprint 8 (SEC-08).
        group.MapPost("/variants/{id:long}/void", async (long id, VoidGtinRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new VoidGtinCommand(id, body.ReasonId, body.Note, body.ActingPerson), cancellationToken)))
            .RequireAuthorization(Policies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapGet("/capacity", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetGtinCapacityQuery(), cancellationToken)));
    }
}
