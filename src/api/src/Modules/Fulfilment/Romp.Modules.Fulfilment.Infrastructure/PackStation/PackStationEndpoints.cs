using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Fulfilment.Application.PackStation;
using Romp.Modules.Fulfilment.Infrastructure.PickLists;

namespace Romp.Modules.Fulfilment.Infrastructure.PackStation;

/// <summary>Body of POST /pack/weight: the parcel's weight in whole grams.</summary>
public sealed record WeightRequest(int Grams);

/// <summary>Body of POST /pack/complete: the packer's note, needed only when the weight does not fit the order.</summary>
public sealed record CompletePackRequest(string? Note);

/// <summary>
/// The pack station (pack-station spec). Reads need <c>fulfilment.view</c>; every action needs <c>fulfilment.pack</c>, checked on every request.
/// The staff member comes from the session (PAK-5). The cash to collect is shown for cash on delivery orders (PAK-11); no other money or customer data.
/// </summary>
internal static class PackStationEndpoints
{
    private const long MaxBodyBytes = 1024;

    public static void MapPackStationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/fulfilment/orders", Permissions.FulfilmentView).WithTags("Fulfilment")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/{id:long}/pack", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetPackViewQuery(id), cancellationToken) is { } view ? Results.Ok(view) : Results.NotFound());

        group.MapPost("/{id:long}/pack/open", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            var opened = await sender.Send(new OpenPackCommand(id, actor.UserName), cancellationToken);
            return opened is null ? Results.NotFound()
                : opened.View is { } view ? Results.Ok(view)
                : Results.Conflict(new { title = $"{opened.HeldBy} already holds this parcel.", heldBy = opened.HeldBy });
        }).RequirePermission(Permissions.FulfilmentPack);

        group.MapPost("/{id:long}/pack/scan", async (long id, ScanRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new ScanPackCommand(id, actor.UserName, body.Code ?? string.Empty), cancellationToken) is { } outcome ? Results.Ok(outcome) : Results.NotFound())
            .RequirePermission(Permissions.FulfilmentPack);

        group.MapPost("/{id:long}/pack/weight", async (long id, WeightRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new RecordWeightCommand(id, actor.UserName, body.Grams), cancellationToken) is { } outcome ? Results.Ok(outcome) : Results.NotFound())
            .RequirePermission(Permissions.FulfilmentPack);

        group.MapPost("/{id:long}/pack/complete", async (long id, CompletePackRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new CompletePackCommand(id, actor.UserName, body.Note), cancellationToken) is { } changed
                ? Results.Ok(new { packed = true, alreadyDone = !changed })
                : Results.NotFound()).RequirePermission(Permissions.FulfilmentPack);
    }
}
