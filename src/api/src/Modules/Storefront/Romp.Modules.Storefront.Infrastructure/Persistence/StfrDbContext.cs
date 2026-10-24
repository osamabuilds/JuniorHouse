using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Modules.Storefront.Infrastructure.Persistence;

/// <summary>EF Core context for the <c>STFR</c> schema (storefront settings and the published snapshot, ADR 0020).</summary>
public sealed class StfrDbContext(DbContextOptions<StfrDbContext> options) : DbContext(options), IStorefrontDbContext
{
    async Task IStorefrontDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("STFR");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StfrDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
