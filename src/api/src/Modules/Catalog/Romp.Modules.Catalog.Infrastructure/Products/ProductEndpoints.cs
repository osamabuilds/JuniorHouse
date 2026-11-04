using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Catalog.Application.Products;

namespace Romp.Modules.Catalog.Infrastructure.Products;

/// <summary>Body of POST /api/catalog/products.</summary>
public sealed record CreateProductRequest(long StyleId, string Name, string Description, short GenderId, short CategoryId, short AgeBracketId, string ActingPerson);

/// <summary>Body of PUT /api/catalog/products/{id}. <c>Version</c> is the token from the last read (PRD-5).</summary>
public sealed record UpdateProductContentRequest(
    string Name, string Description, short GenderId, short CategoryId, short AgeBracketId, string? CareNote, uint Version, string ActingPerson);

/// <summary>Body of PUT /api/catalog/products/{id}/prices.</summary>
public sealed record SetVariantPricesRequest(IReadOnlyList<VariantPriceInput> Prices, uint Version, string ActingPerson);

/// <summary>Body of PUT /api/catalog/products/{id}/collections.</summary>
public sealed record SetCollectionsRequest(IReadOnlyList<short> CollectionIds, uint Version, string ActingPerson);

/// <summary>Body of the publish, unpublish and archive POSTs.</summary>
public sealed record ProductActionRequest(uint Version, string ActingPerson);

// Mapped through MapSecuredGroup with the Catalogue policy (ADR 0011, PRD-9, SEC-08).
internal static class ProductEndpoints
{
    private const long MaxBodyBytes = 64 * 1024;

    public static void MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/catalog/products", Permissions.CatalogRead).WithTags("Catalog")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/", async (
                string? search,
                short? statusId,
                short? categoryId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListProductsQuery(search, statusId, categoryId, page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetProductQuery(id), cancellationToken)));

        group.MapPost("/", async (CreateProductRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new CreateProductCommand(body.StyleId, body.Name, body.Description, body.GenderId, body.CategoryId, body.AgeBracketId, body.ActingPerson),
                cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/{id:long}", async (long id, UpdateProductContentRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new UpdateProductContentCommand(id, body.Name, body.Description, body.GenderId, body.CategoryId, body.AgeBracketId, body.CareNote, body.Version, body.ActingPerson),
                cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/{id:long}/prices", async (long id, SetVariantPricesRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetVariantPricesCommand(id, body.Prices, body.Version, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/{id:long}/collections", async (long id, SetCollectionsRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetCollectionsCommand(id, body.CollectionIds, body.Version, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        // A refused publish is 422 with every failure (PRD-4); the editor lists them as links to the tabs that fix them.
        group.MapPost("/{id:long}/publish", async (long id, ProductActionRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new PublishProductCommand(id, body.Version, body.ActingPerson), cancellationToken);
                return result.Published ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity);
            }).RequirePermission(Permissions.CatalogWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/{id:long}/unpublish", async (long id, ProductActionRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new UnpublishProductCommand(id, body.Version, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.CatalogWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/{id:long}/archive", async (long id, ProductActionRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ArchiveProductCommand(id, body.Version, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.CatalogWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/{id:long}/refresh-variants", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RefreshVariantsFromLabellingCommand(id), cancellationToken))).RequirePermission(Permissions.CatalogWrite);
    }
}
