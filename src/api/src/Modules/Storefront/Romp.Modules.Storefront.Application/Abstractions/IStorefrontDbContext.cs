namespace Romp.Modules.Storefront.Application.Abstractions;

/// <summary>Application's own port onto the <c>STFR</c> store, so handlers never depend on Infrastructure (ADR 0002). Sets are added with each feature.</summary>
public interface IStorefrontDbContext
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
