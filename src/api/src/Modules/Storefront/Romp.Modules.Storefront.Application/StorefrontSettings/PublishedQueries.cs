using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// The current snapshot split into what the public routes serve: the settings, each published page, the 301s and the 410 addresses.
/// Held in the 60-second cache (ST-13).
/// </summary>
public sealed record PublishedDocument(
    int VersionNo,
    string Hash,
    DateTimeOffset PublishedAt,
    string SettingsJson,
    IReadOnlyDictionary<string, string> PagesJson,
    IReadOnlyDictionary<string, string> Redirects,
    IReadOnlySet<string> GoneSlugs)
{
    public string ETag => $"\"{Hash}\"";

    public static PublishedDocument Parse(int versionNo, string hash, DateTimeOffset publishedAt, string document)
    {
        var root = JsonNode.Parse(document)!;
        return new PublishedDocument(
            versionNo,
            hash,
            publishedAt,
            root["settings"]!.ToJsonString(),
            root["pages"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.ToJsonString(), StringComparer.Ordinal),
            root["redirects"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal),
            root["gone"]!.AsArray().Select(s => s!.GetValue<string>()).ToHashSet(StringComparer.Ordinal));
    }
}

/// <summary>The settings JSON of the current snapshot (cached 60 s), or null before anything was published.</summary>
public sealed record GetPublishedSettingsQuery : IRequest<PublishedSettingsDto?>;

public sealed record PublishedSettingsDto(string Json, string ETag, int VersionNo);

/// <summary>A public page by slug: found, redirected to its new address (301), permanently removed (410) or unknown (404).</summary>
public sealed record GetPublishedPageQuery(string Slug) : IRequest<PublishedPageResult>;

public enum PublishedPageOutcome
{
    Found,
    Redirect,
    Gone,
    NotFound,
}

public sealed record PublishedPageResult(PublishedPageOutcome Outcome, string? Json = null, string? ETag = null, string? RedirectSlug = null);

internal static class PublishedDocumentLoader
{
    public static Task<PublishedDocument?> GetAsync(IStorefrontDbContext db, IPublishedDocumentCache cache, CancellationToken cancellationToken) =>
        cache.GetOrLoadAsync(async ct =>
        {
            var current = await db.Snapshots.AsNoTracking().Where(s => s.IsCurrent)
                .Select(s => new { s.VersionNo, s.DocumentHash, s.InsrDte, s.Document }).FirstOrDefaultAsync(ct);
            return current is null ? null : PublishedDocument.Parse(current.VersionNo, current.DocumentHash, current.InsrDte, current.Document);
        }, cancellationToken);
}

public sealed class GetPublishedSettingsQueryHandler(IStorefrontDbContext db, IPublishedDocumentCache cache)
    : IRequestHandler<GetPublishedSettingsQuery, PublishedSettingsDto?>
{
    public async Task<PublishedSettingsDto?> Handle(GetPublishedSettingsQuery request, CancellationToken cancellationToken)
    {
        var document = await PublishedDocumentLoader.GetAsync(db, cache, cancellationToken);
        return document is null ? null : new PublishedSettingsDto(document.SettingsJson, document.ETag, document.VersionNo);
    }
}

public sealed class GetPublishedPageQueryHandler(IStorefrontDbContext db, IPublishedDocumentCache cache)
    : IRequestHandler<GetPublishedPageQuery, PublishedPageResult>
{
    public async Task<PublishedPageResult> Handle(GetPublishedPageQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();
        var document = await PublishedDocumentLoader.GetAsync(db, cache, cancellationToken);
        if (document is null)
        {
            return new PublishedPageResult(PublishedPageOutcome.NotFound);
        }

        if (document.PagesJson.TryGetValue(slug, out var json))
        {
            return new PublishedPageResult(PublishedPageOutcome.Found, json, document.ETag);
        }

        if (document.Redirects.TryGetValue(slug, out var target))
        {
            return new PublishedPageResult(PublishedPageOutcome.Redirect, RedirectSlug: target);
        }

        return new PublishedPageResult(document.GoneSlugs.Contains(slug) ? PublishedPageOutcome.Gone : PublishedPageOutcome.NotFound);
    }
}

/// <summary>The published pages for the sitemap, from the current snapshot (the 60 s copy), through the Storefront contract.</summary>
public sealed class PublishedPageSitemapSource(IStorefrontDbContext db, IPublishedDocumentCache cache) : Contracts.IStorefrontPageQueries
{
    public async Task<IReadOnlyList<Contracts.PublishedPageRef>> ListPublishedPagesAsync(CancellationToken cancellationToken)
    {
        var document = await PublishedDocumentLoader.GetAsync(db, cache, cancellationToken);
        if (document is null)
        {
            return [];
        }

        return [.. document.PagesJson
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new Contracts.PublishedPageRef(p.Key, JsonNode.Parse(p.Value)!["title"]!.GetValue<string>(), document.PublishedAt))];
    }
}
