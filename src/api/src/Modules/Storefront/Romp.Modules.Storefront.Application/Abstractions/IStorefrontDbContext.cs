using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Storefront so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IStorefrontCommand;

/// <summary>
/// Marks a DTO that an anonymous visitor can receive. <c>PublicResponseTests</c> walks every type that carries the marker and refuses an exact
/// stock quantity (published-catalog PCT-3, Q-PCT-2): shoppers see an <c>AvailabilityClass</c>, never a count.
/// </summary>
public interface IPublicStorefrontResponse;

/// <summary>Application's own port onto the <c>STFR</c> store, so handlers never depend on Infrastructure (ADR 0002).</summary>
public interface IStorefrontDbContext
{
    DbSet<NavItem> NavItems { get; }

    DbSet<AgeStep> AgeSteps { get; }

    DbSet<FooterColumn> FooterColumns { get; }

    DbSet<SettingItem> SettingItems { get; }

    DbSet<PopularSearch> PopularSearches { get; }

    DbSet<DeliveryRule> DeliveryRules { get; }

    DbSet<ExchangePolicy> ExchangePolicies { get; }

    DbSet<Page> Pages { get; }

    DbSet<PageRedirect> PageRedirects { get; }

    DbSet<SettingsDraft> Drafts { get; }

    DbSet<PublishedSnapshot> Snapshots { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>The PostgreSQL row version (<c>xmin</c>) of a loaded page; the client quotes it back on its next save (ST-14).</summary>
    uint RowVersionOf(Page page);

    /// <summary>
    /// Makes the next save of the page conditional on the row version the client loaded, and always writes the page row, so a change to
    /// only its blocks still conflicts with someone else's save (ST-14).
    /// </summary>
    void RequireRowVersion(Page page, uint expectedRowVersion);
}

/// <summary>The external sites a link may point at, from configuration (plan: outbound calls and links are allow-listed).</summary>
public interface IExternalHostPolicy
{
    IReadOnlyCollection<string> AllowedHosts { get; }
}

/// <summary>
/// The 60-second copy of the current published document the public routes read (ST-13). Dropped when a new snapshot is published.
/// </summary>
public interface IPublishedDocumentCache
{
    Task<PublishedDocument?> GetOrLoadAsync(Func<CancellationToken, Task<PublishedDocument?>> load, CancellationToken cancellationToken);

    void Invalidate();
}
