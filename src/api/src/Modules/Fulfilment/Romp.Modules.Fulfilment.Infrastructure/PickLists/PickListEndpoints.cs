using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Fulfilment.Application.PickLists;

namespace Romp.Modules.Fulfilment.Infrastructure.PickLists;

/// <summary>Body of POST /pick/scan: one bin code or one barcode, as the scanner typed it.</summary>
public sealed record ScanRequest(string? Code);

/// <summary>
/// The pick queue and the pick station (pick-lists spec). Every route is in <c>MapSecuredGroup</c> with the permission checked on every request
/// (SEC-08, APP-1): <c>fulfilment.view</c> to read, <c>fulfilment.pick</c> to act. The acting staff member comes from the signed-in account, never
/// from the body (PCK-10). Nothing here takes a customer id, and no response carries customer data (PCK-11).
/// </summary>
internal static class PickListEndpoints
{
    private const long MaxBodyBytes = 1024;

    public static void MapPickListEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapSecuredGroup("/api/fulfilment", Permissions.FulfilmentView).WithTags("Fulfilment");

        read.MapGet("/queue", async (ISender sender, CancellationToken cancellationToken, string? stage = null, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListQueueQuery(ParseStage(stage), page, pageSize), cancellationToken)));

        read.MapGet("/orders/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetPickListQuery(id), cancellationToken) is { } list ? Results.Ok(list) : Results.NotFound());

        var pick = endpoints.MapSecuredGroup("/api/fulfilment/orders", Permissions.FulfilmentView).WithTags("Fulfilment")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        pick.MapPost("/{id:long}/pick/claim", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ClaimPickCommand(id, actor.UserName), cancellationToken);
            return result.NotFound ? Results.NotFound()
                : result.Claimed ? Results.Ok(await sender.Send(new GetPickListQuery(id), cancellationToken))
                : Results.Conflict(new { title = $"{result.HeldBy} already holds this pick.", heldBy = result.HeldBy });
        }).RequirePermission(Permissions.FulfilmentPick);

        pick.MapPost("/{id:long}/pick/release", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new GetPickListQuery(id), cancellationToken) is null
                ? Results.NotFound()
                : Results.Ok(new { released = await sender.Send(new ReleasePickClaimCommand(id, actor.UserName, false), cancellationToken) })).RequirePermission(Permissions.FulfilmentPick);

        pick.MapPost("/{id:long}/pick/take-over", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new GetPickListQuery(id), cancellationToken) is null
                ? Results.NotFound()
                : Results.Ok(new { released = await sender.Send(new ReleasePickClaimCommand(id, actor.UserName, true), cancellationToken) })).RequirePermission(Permissions.FulfilmentManage);

        pick.MapPost("/{id:long}/pick/scan", async (long id, ScanRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new ScanPickCommand(id, actor.UserName, body.Code ?? string.Empty), cancellationToken) is { } outcome ? Results.Ok(outcome) : Results.NotFound())
            .RequirePermission(Permissions.FulfilmentPick);

        pick.MapPost("/{id:long}/pick/complete", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new CompletePickCommand(id, actor.UserName), cancellationToken) is { } changed
                ? Results.Ok(new { completed = true, alreadyDone = !changed })
                : Results.NotFound()).RequirePermission(Permissions.FulfilmentPick);
    }

    private static QueueStage ParseStage(string? stage) => stage?.Trim().ToLowerInvariant() switch
    {
        "pack" => QueueStage.Pack,
        "held" => QueueStage.Held,
        "put-back" => QueueStage.PutBack,
        _ => QueueStage.Pick,
    };
}
