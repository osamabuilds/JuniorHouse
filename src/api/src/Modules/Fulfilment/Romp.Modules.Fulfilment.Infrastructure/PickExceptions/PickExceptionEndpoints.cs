using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Fulfilment.Application.PickExceptions;

namespace Romp.Modules.Fulfilment.Infrastructure.PickExceptions;

/// <summary>Body of POST /pick/exceptions: which line, at which bin, why, and how many units the picker counted.</summary>
public sealed record RaiseExceptionRequest(long VariantId, string? BinCode, string? Reason, int FoundCount);

/// <summary>Body of POST /exceptions/{id}/resolve: <c>restocked</c> or <c>cancel-line</c>.</summary>
public sealed record ResolveExceptionRequest(string? Action);

/// <summary>
/// Pick exceptions (pick-exceptions spec): the picker raises one instead of force-completing (<c>fulfilment.pick</c>); staff read the queue
/// (<c>fulfilment.view</c>) and resolve (<c>fulfilment.manage</c>). Permissions are checked on every request; the staff member comes from the session.
/// </summary>
internal static class PickExceptionEndpoints
{
    private const long MaxBodyBytes = 1024;

    public static void MapPickExceptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/fulfilment", Permissions.FulfilmentView).WithTags("Fulfilment")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/exceptions", async (ISender sender, CancellationToken cancellationToken, bool openOnly = true, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListExceptionsQuery(openOnly, page, pageSize), cancellationToken)));

        group.MapPost("/orders/{id:long}/pick/exceptions", async (long id, RaiseExceptionRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new RaiseExceptionCommand(id, actor.UserName, body.VariantId, body.BinCode ?? string.Empty, body.Reason ?? string.Empty, body.FoundCount), cancellationToken) is { } raised
                ? Results.Ok(raised)
                : Results.NotFound()).RequirePermission(Permissions.FulfilmentPick);

        group.MapPost("/exceptions/{id:long}/resolve", async (long id, ResolveExceptionRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            ResolveAction? action = body.Action?.Trim().ToLowerInvariant() switch
            {
                "restocked" => ResolveAction.Restocked,
                "cancel-line" => ResolveAction.CancelLine,
                _ => null,
            };
            if (action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["Choose restocked or cancel-line."] });
            }

            return await sender.Send(new ResolveExceptionCommand(id, action.Value, actor.UserName), cancellationToken) is { } resolved
                ? Results.Ok(resolved)
                : Results.NotFound();
        }).RequirePermission(Permissions.FulfilmentManage);
    }
}
