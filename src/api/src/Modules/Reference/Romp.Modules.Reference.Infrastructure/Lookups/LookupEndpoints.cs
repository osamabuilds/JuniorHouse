using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Reference.Application.Lookups;
using Romp.Modules.Reference.Domain.Lookups;

namespace Romp.Modules.Reference.Infrastructure.Lookups;

/// <summary>
/// Maps the <c>GET /api/ref/{lookup}</c> read endpoint every lookup type gets, plus the
/// create/update/retire endpoints for the ten staff-maintained ones (everything except
/// <c>po-statuses</c>, AC-1/AC-2). One generic method instead of eleven near-identical endpoint
/// blocks.
/// </summary>
internal static class LookupEndpoints
{
    public static void MapLookupType<TLookup>(this IEndpointRouteBuilder endpoints, string routeSegment, bool mutable)
        where TLookup : Lookup
    {
        var group = endpoints.MapGroup($"/api/ref/{routeSegment}").WithTags("Reference");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken, bool includeInactive = false) =>
            Results.Ok(await sender.Send(new ListLookupQuery<TLookup>(includeInactive), cancellationToken)));

        if (!mutable)
        {
            return;
        }

        group.MapPost("/", async (CreateLookupCommand<TLookup> command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken)));

        group.MapPut("/{id}", async (
                short id,
                UpdateLookupRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new UpdateLookupCommand<TLookup>(id, body.Name, body.Description, body.SortSeq, body.ParentCategoryId, body.DefaultAdvancePercent),
                cancellationToken)));

        group.MapPost("/{id}/retire", async (short id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RetireLookupCommand<TLookup>(id), cancellationToken);
            return Results.NoContent();
        });
    }
}
