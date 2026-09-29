using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Romp.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Design-time factory (EF Core's Abstract Factory hook, <see cref="IDesignTimeDbContextFactory{TContext}"/>)
/// that lets the `dotnet ef migrations` CLI build a <see cref="CatalogDbContext"/> without going
/// through the API host's DI container, which the CLI tooling never runs.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        // No DI container at design time, so the CLI can't read appsettings/user-secrets the way
        // the running API does; ConnectionStrings__Postgres (same variable docker-compose sets for
        // the API container) is the one override point, falling back to the local default that
        // matches .env.example's placeholder credentials.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CTLG"));

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
