using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.Revisions;

internal static class RevisionEndpoints
{
    public static void MapRevisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Vendor");

        // SCRUM-93 task 29.
        group.MapPost("/{id:long}/amendments", async (
                long id,
                CreateAmendmentRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/amendments/{revNo}/accept", async (
                long id,
                short revNo,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecideRevisionCommand(id, revNo, Accept: true, Note: null), cancellationToken)));

        group.MapPost("/{id:long}/amendments/{revNo}/reject", async (
                long id,
                short revNo,
                RevisionNoteRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecideRevisionCommand(id, revNo, Accept: false, body.Note), cancellationToken)));

        group.MapPost("/{id:long}/amendments/{revNo}/withdraw", async (
                long id,
                short revNo,
                RevisionNoteRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new WithdrawRevisionCommand(id, revNo, body.Note), cancellationToken)));

        group.MapGet("/{id:long}/revisions", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetPurchaseOrderRevisionsQuery(id), cancellationToken)));
    }
}
