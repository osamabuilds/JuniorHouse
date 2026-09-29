using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>CTLG</c> schema (style master). Implements
/// <see cref="ICatalogDbContext"/> (Application's own abstraction) so Application-layer command
/// and query handlers can use it without depending on this Infrastructure project (ADR 0002).
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    public DbSet<Style> Styles => Set<Style>();

    async Task ICatalogDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CTLG");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
