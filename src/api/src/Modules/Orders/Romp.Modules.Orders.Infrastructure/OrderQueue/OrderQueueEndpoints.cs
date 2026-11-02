using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.OrderQueue;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Infrastructure.OrderQueue;

/// <summary>How a confirmation call ended: <c>confirmed</c>, <c>no-answer</c> or <c>refused</c> (COD-3).</summary>
public sealed record RecordOutcomeRequest(string? Outcome, string? Note);

public sealed record CancelRequest(string? Note);

/// <summary>
/// The staff order queue (orders Q1 to Q3). Every route is in <c>MapSecuredGroup</c> with the permission checked on every request (SEC-08):
/// <c>orders.view</c> to read, <c>orders.manage</c> to change. The acting staff member comes from the signed-in account, never from the body,
/// and each action is recorded in the order history and the audit trail. A change to an order that moved meanwhile answers 409.
/// </summary>
internal static class OrderQueueEndpoints
{
    private const long MaxBodyBytes = 2 * 1024;

    public static void MapOrderQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSecuredGroup("/api/admin/orders", Permissions.OrdersView).WithTags("Orders")
            .MapGet("", async (
                ISender sender,
                CancellationToken cancellationToken,
                short? status = null,
                short? method = null,
                DateTimeOffset? from = null,
                DateTimeOffset? to = null,
                bool callDueNow = false,
                bool flagged = false,
                string? search = null,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
                Results.Ok(await sender.Send(new ListOrdersQuery(status, method, from, to, callDueNow, flagged, search, page, pageSize), cancellationToken)));

        endpoints.MapSecuredGroup("/api/admin/orders", Permissions.OrdersView).WithTags("Orders")
            .MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
                await sender.Send(new GetOrderQuery(id), cancellationToken) is { } found ? Results.Ok(found) : Results.NotFound());

        var manage = endpoints.MapSecuredGroup("/api/admin/orders", Permissions.OrdersManage).WithTags("Orders")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        manage.MapPost("/{id:long}/cod-outcome", async (long id, RecordOutcomeRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            var outcome = body.Outcome?.Trim().ToLowerInvariant() switch
            {
                "confirmed" => CodOutcomes.Confirmed,
                "no-answer" => CodOutcomes.NoAnswer,
                "refused" => CodOutcomes.Refused,
                _ => (short)0,
            };
            if (outcome == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["outcome"] = ["Choose confirmed, no answer or refused."] });
            }

            if (await sender.Send(new GetOrderQuery(id), cancellationToken) is not { } order)
            {
                return Results.NotFound();
            }

            var result = await sender.Send(new RecordCodOutcomeCommand(order.OrderNumber, outcome, actor.UserName, body.Note), cancellationToken);
            return result.Kind switch
            {
                CodOutcomeKind.Recorded => Results.Ok(await sender.Send(new GetOrderQuery(id), cancellationToken)),
                CodOutcomeKind.NotFound => Results.NotFound(),
                _ => Results.Conflict(new { title = "This order is no longer waiting for a confirmation call." }),
            };
        });

        manage.MapPost("/{id:long}/cancel", async (long id, CancelRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            if (await sender.Send(new GetOrderQuery(id), cancellationToken) is not { } order)
            {
                return Results.NotFound();
            }

            var result = await sender.Send(new CancelOrderCommand(order.OrderNumber, CancelReasons.Staff, ActorKinds.Staff, actor.UserName, body.Note), cancellationToken);
            return result.Outcome switch
            {
                CancelOrderOutcome.Cancelled or CancelOrderOutcome.AlreadyCancelled => Results.Ok(await sender.Send(new GetOrderQuery(id), cancellationToken)),
                CancelOrderOutcome.NotFound => Results.NotFound(),
                _ => Results.Conflict(new { title = result.Message ?? "This order cannot be cancelled." }),
            };
        });

        manage.MapPost("/{id:long}/clear-flag", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            if (await sender.Send(new GetOrderQuery(id), cancellationToken) is not { } order)
            {
                return Results.NotFound();
            }

            await sender.Send(new ClearCodFlagCommand(order.ContactMobile, actor.UserName), cancellationToken);
            return Results.Ok(await sender.Send(new GetOrderQuery(id), cancellationToken));
        });
    }
}
