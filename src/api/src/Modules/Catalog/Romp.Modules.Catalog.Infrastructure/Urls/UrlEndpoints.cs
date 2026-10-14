using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Catalog.Application.Urls;

namespace Romp.Modules.Catalog.Infrastructure.Urls;

/// <summary>Body of POST /api/catalog/slugs/suggest.</summary>
public sealed record SuggestSlugRequest(string Name, short GenderId, short CategoryId, long? ProductId);

/// <summary>Body of PUT /api/catalog/products/{id}/path.</summary>
public sealed record SetProductPathRequest(short GenderId, short CategoryId, string Slug, uint Version, string ActingPerson);

// Mapped through MapSecuredGroup with the Catalogue policy (ADR 0011, SEC-08).
internal static class UrlEndpoints
{
    private const long MaxBodyBytes = 8 * 1024;

    public static void MapUrlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/catalog", Policies.Catalogue).WithTags("Catalog")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapPost("/slugs/suggest", async (SuggestSlugRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SuggestSlugQuery(body.Name, body.GenderId, body.CategoryId, body.ProductId), cancellationToken)));

        group.MapPut("/products/{id:long}/path", async (long id, SetProductPathRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new SetProductPathCommand(id, body.GenderId, body.CategoryId, body.Slug, body.Version, body.ActingPerson), cancellationToken)));

        group.MapGet("/products/{id:long}/path-history", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetPathHistoryQuery(id), cancellationToken)));

        // Staff can ask what a public path means. The storefront (S9) uses IProductPathQueries in-process instead.
        group.MapGet("/paths/resolve", async ([FromQuery] string path, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ResolvePathQuery(path), cancellationToken)));
    }
}
