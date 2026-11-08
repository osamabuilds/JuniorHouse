using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.ProductRecommendations;
using Romp.Modules.Storefront.Domain.ProductPages;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.ProductRecommendations;

/// <summary>The body of a recommendation event: which row, which product, what happened (REC-12). Nothing about the person.</summary>
public sealed record RecommendationEventRequest(
    [property: JsonPropertyName("row")] string? Row,
    [property: JsonPropertyName("productId")] long ProductId,
    [property: JsonPropertyName("action")] string? Action);

/// <summary>
/// The recommendations of a product page and the event counter behind its cards (REC-1, REC-12). Both are anonymous: the read returns public
/// catalogue data, and the counter reads nothing and keeps no person. Errors are problem details.
/// </summary>
public static class RecommendationEndpoints
{
    /// <summary>The event body is a few dozen bytes; anything over this is refused before it is read.</summary>
    public const int MaxEventBodyBytes = 1024;

    public static void MapPublicRecommendationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(StorefrontSettingsEndpoints.PublicPrefix + "/products/recommendations", Policies.Public).WithTags("Storefront");

        group.MapGet(string.Empty, async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var path = http.Request.Query.TryGetValue("path", out var p) && p.Count > 0 ? p[0] : null;
            if (ProductPath.Normalise(path) is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["path"] = ["A valid product path is required."] });
            }

            var result = await sender.Send(new GetProductRecommendationsQuery(path), cancellationToken);
            if (result is null)
            {
                http.Response.Headers[HeaderNames.CacheControl] = "no-store";
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found.");
            }

            // Nothing cached beyond the browser: a sold-out product must leave the row at once (REC-5).
            http.Response.Headers[HeaderNames.CacheControl] = "no-cache";
            return Results.Ok(result);
        });

        group.MapPost("/events", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
            {
                // Kestrel enforces the size limit below for every request; this also refuses a declared length over it before the body is read.
                if (http.Request.ContentLength > MaxEventBodyBytes)
                {
                    return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "The request is too large.");
                }

                RecommendationEventRequest? body;
                try
                {
                    body = await http.Request.ReadFromJsonAsync<RecommendationEventRequest>(cancellationToken);
                }
                catch (Exception ex) when (ex is JsonException or BadHttpRequestException or InvalidOperationException)
                {
                    body = null;
                }

                if (body is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["event"] = ["A JSON body with row, productId and action is required."] });
                }

                var outcome = await sender.Send(new RecordRecommendationEventCommand(body.Row, body.ProductId, body.Action), cancellationToken);
                return outcome switch
                {
                    RecommendationEventOutcome.Recorded => Results.NoContent(),
                    RecommendationEventOutcome.UnknownProduct => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found."),
                    _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["event"] = ["Row must be SIMILAR or TRENDING, action click or add, and the product id positive."] }),
                };
            })
            .WithMetadata(new RequestSizeLimitAttribute(MaxEventBodyBytes))
            .RequireRateLimiting(RateLimitPolicies.Strict);
    }
}
