using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Shipping.Application.Manifests;

namespace Romp.Modules.Shipping.Infrastructure.Manifests;

public sealed record CountRequest(int Parcels);

public sealed record HandoverRequest(long ShipmentId, bool HandedOver);

public sealed record ResolveRequest(string? Note);

/// <summary>
/// The manifests (manifests spec): read with <c>shipping.view</c>; make, count, mark and resolve with <c>shipping.ops</c>. The staff member comes from the
/// session. Counting and resolving are the audit trail of "the courier says they never received it" (MNF-9).
/// </summary>
internal static class ManifestEndpoints
{
    public static void MapManifestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/shipping/manifests", Permissions.ShippingView).WithTags("Shipping")
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        group.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListManifestsQuery(page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetManifestQuery(id), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound());

        group.MapPost("/generate", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GenerateManifestsCommand(), cancellationToken))).RequirePermission(Permissions.ShippingOps);

        group.MapPost("/{id:long}/count", async (long id, CountRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new CountManifestCommand(id, body.Parcels, actor.UserName), cancellationToken) is { } summary ? Results.Ok(summary) : Results.NotFound())
            .RequirePermission(Permissions.ShippingOps);

        group.MapPost("/{id:long}/handover", async (long id, HandoverRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new MarkHandedOverCommand(id, body.ShipmentId, body.HandedOver, actor.UserName), cancellationToken)
                ? Results.Ok(await sender.Send(new GetManifestQuery(id), cancellationToken))
                : Results.NotFound()).RequirePermission(Permissions.ShippingOps);

        group.MapPost("/{id:long}/resolve", async (long id, ResolveRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new ResolveManifestCommand(id, body.Note ?? string.Empty, actor.UserName), cancellationToken) is { } summary ? Results.Ok(summary) : Results.NotFound())
            .RequirePermission(Permissions.ShippingOps);
    }
}
