using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.ProductMedia;

namespace Romp.Modules.Catalog.Infrastructure.ProductMedia;

/// <summary>Body of PUT /api/catalog/images/{id}/alt.</summary>
public sealed record SetImageAltRequest(string? Text, string ActingPerson);

/// <summary>Body of PUT /api/catalog/products/{id}/images/order.</summary>
public sealed record ReorderImagesRequest(IReadOnlyList<long> ImageIds, string ActingPerson);

// Mapped through MapSecuredGroup with the Catalogue policy (ADR 0011, SEC-08).
internal static class ProductMediaEndpoints
{
    /// <summary>Room above the image limit for the multipart envelope and the small text fields.</summary>
    private const long MultipartOverheadBytes = 64 * 1024;

    public static void MapProductMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/catalog", Policies.Catalogue).WithTags("Catalog");

        group.MapGet("/products/{id:long}/images", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListProductImagesQuery(id), cancellationToken)));

        // MED-7, MED-9: one file part; the type is decided by the bytes; the body is capped at the image limit plus the envelope.
        group.MapPost("/products/{id:long}/images", async (
                long id,
                HttpContext http,
                [FromForm] string actingPerson,
                IFormFile file,
                ISender sender,
                IOptions<CatalogOptions> options,
                CancellationToken cancellationToken,
                [FromForm] short? colourId = null,
                [FromForm] string? altText = null) =>
            {
                if (file.Length > options.Value.MaxImageBytes)
                {
                    return Results.Problem(
                        title: "The image is too large",
                        detail: $"The limit is {options.Value.MaxImageBytes / 1024 / 1024.0:0.#} MB.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                return Results.Ok(await sender.Send(
                    new UploadProductImageCommand(id, buffer.ToArray(), file.FileName, colourId, altText, actingPerson), cancellationToken));
            })
            .AddEndpointFilter(async (context, next) =>
            {
                var limit = context.HttpContext.RequestServices.GetRequiredService<IOptions<CatalogOptions>>().Value.MaxImageBytes + MultipartOverheadBytes;
                var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false })
                {
                    feature.MaxRequestBodySize = limit;
                }

                return await next(context);
            })
            .RequireRateLimiting(RateLimitPolicies.Strict)
            .DisableAntiforgery();

        group.MapPut("/images/{imageId:long}/alt", async (long imageId, SetImageAltRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetImageAltCommand(imageId, body.Text, body.ActingPerson), cancellationToken)));

        group.MapPut("/products/{id:long}/images/order", async (long id, ReorderImagesRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ReorderImagesCommand(id, body.ImageIds, body.ActingPerson), cancellationToken)));

        group.MapDelete("/images/{imageId:long}", async (long imageId, [FromQuery] string actingPerson, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteImageCommand(imageId, actingPerson), cancellationToken);
            return Results.NoContent();
        });

        // MED-9: the file is served from our own record of its type, never the client's; the browser may not guess a different one.
        group.MapGet("/images/{imageId:long}/file", async (long imageId, HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var file = await sender.Send(new GetImageFileQuery(imageId), cancellationToken);
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers["Content-Disposition"] = "inline";
            http.Response.Headers["Cache-Control"] = "private, max-age=300";
            return Results.File(file.Content, file.ContentType);
        });
    }
}
