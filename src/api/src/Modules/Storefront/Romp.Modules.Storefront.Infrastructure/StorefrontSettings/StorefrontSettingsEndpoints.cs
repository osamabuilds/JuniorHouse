using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

/// <summary>Body of PUT /api/admin/storefront-settings/pages/{id}. The id comes from the route.</summary>
public sealed record UpdatePageRequest(uint RowVersion, string Slug, string Title, string MetaDescription, IReadOnlyList<PageBlockDto> Blocks);

/// <summary>Body of PUT /api/admin/storefront-settings/pages/{id}/status.</summary>
public sealed record ChangePageStatusRequest(uint RowVersion, PageStatus Status);

/// <summary>
/// The public reads (anonymous, cached) and the admin API (role-checked on every request, SEC-08). Both groups go through
/// <c>MapSecuredGroup</c>, so each states who may call it (ADR 0011).
/// </summary>
public static class StorefrontSettingsEndpoints
{
    public const string AdminPrefix = "/api/admin/storefront-settings";
    public const string PublicPrefix = "/api/storefront";

    // A full page is at most 60 blocks of 5,000 characters; the navigation is at most 100 entries.
    private const long MaxBodyBytes = 1024 * 1024;

    public static void MapPublicStorefrontEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(PublicPrefix, Policies.Public).WithTags("Storefront");

        group.MapGet("/settings", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var settings = await sender.Send(new GetPublishedSettingsQuery(), cancellationToken);
            return settings is null ? Results.NotFound() : Cached(http, settings.Json, settings.ETag);
        });

        group.MapGet("/pages/{slug}", async (string slug, HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            if (slug.Length > 60)
            {
                return Results.NotFound();
            }

            var page = await sender.Send(new GetPublishedPageQuery(slug), cancellationToken);
            switch (page.Outcome)
            {
                case PublishedPageOutcome.Found:
                    return Cached(http, page.Json!, page.ETag!);
                case PublishedPageOutcome.Redirect:
                    // 301: the old address now belongs to another slug (ST-9). The storefront turns this into its own 301.
                    http.Response.Headers.Location = $"{PublicPrefix}/pages/{page.RedirectSlug}";
                    return Results.Json(new { slug = page.RedirectSlug }, statusCode: StatusCodes.Status301MovedPermanently);
                case PublishedPageOutcome.Gone:
                    return Results.StatusCode(StatusCodes.Status410Gone);
                default:
                    return Results.NotFound();
            }
        });
    }

    public static void MapAdminStorefrontSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(AdminPrefix, Permissions.CatalogRead).WithTags("Storefront settings")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/draft", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetDraftSettingsQuery(), cancellationToken)));

        group.MapPut("/navigation", async (SaveNavigationCommand body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/ages", async (SaveAgeStepsCommand body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/footer", async (SaveFooterCommand body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/contact", async (SaveContactSettingsCommand body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/delivery", async (SaveDeliveryAndExchangeCommand body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapGet("/pages", async (
                ISender sender,
                CancellationToken cancellationToken,
                PageStatus? status = null,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListPagesQuery(status, page, pageSize), cancellationToken)));

        group.MapGet("/pages/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetPageQuery(id), cancellationToken) is { } page ? Results.Ok(page) : Results.NotFound());

        group.MapPost("/pages", async (CreatePageCommand body, ISender sender, CancellationToken cancellationToken) =>
        {
            var page = await sender.Send(body, cancellationToken);
            return Results.Created($"{AdminPrefix}/pages/{page.Id}", page);
        }).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/pages/{id:long}", async (long id, UpdatePageRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new UpdatePageCommand(id, body.RowVersion, body.Slug, body.Title, body.MetaDescription, body.Blocks), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/pages/{id:long}/status", async (long id, ChangePageStatusRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ChangePageStatusCommand(id, body.RowVersion, body.Status), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapGet("/preview", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Content(await sender.Send(new PreviewSettingsQuery(), cancellationToken), "application/json"))
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/publish", async (PublishSettingsCommand body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/restore", async (RestoreSnapshotCommand body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(body, cancellationToken))).RequirePermission(Permissions.CatalogWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapGet("/snapshots", async (
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListSnapshotsQuery(page, pageSize), cancellationToken)));
    }

    /// <summary>200 with the JSON, or 304 when the caller already holds this version; both carry the 60 s public cache header.</summary>
    private static IResult Cached(HttpContext http, string json, string etag)
    {
        http.Response.Headers[HeaderNames.ETag] = etag;
        http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
        return http.Request.Headers[HeaderNames.IfNoneMatch].Contains(etag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Content(json, "application/json");
    }
}

/// <summary>
/// The deployment guard for the admin routes (plan: NFR/security design, ADR 0011). They are switched on by <c>Storefront:EnableAdminRoutes</c>;
/// outside Development/Testing the host refuses to start with the switch on unless a real authentication scheme is registered.
/// </summary>
public static class StorefrontAdminRoutesGuard
{
    public static void Verify(bool enabled, bool developmentLike, bool realAuthenticationRegistered, string environmentName)
    {
        if (enabled && !developmentLike && !realAuthenticationRegistered)
        {
            throw new InvalidOperationException(
                $"Refusing to start: the storefront settings admin routes are enabled and the environment is '{environmentName}', " +
                "but no real authentication scheme is registered. Staff authentication (Sprint 8, SEC-08) must be in place first, " +
                "or set Storefront:EnableAdminRoutes to false.");
        }
    }
}
