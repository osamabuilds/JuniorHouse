namespace Romp.Modules.Storefront.Contracts;

/// <summary>A published content page as the sitemap needs it. <see cref="LastModified"/> is when the snapshot holding it was published.</summary>
public sealed record PublishedPageRef(string Slug, string Title, DateTimeOffset LastModified);

/// <summary>
/// What other modules may ask Storefront about its content pages (ADR 0002: through Contracts only). The sitemap job lists the published
/// pages from the current snapshot, so a page appears when it is published and goes when it is archived (SEO-05).
/// </summary>
public interface IStorefrontPageQueries
{
    Task<IReadOnlyList<PublishedPageRef>> ListPublishedPagesAsync(CancellationToken cancellationToken);
}
