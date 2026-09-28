using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// Design-time factory (EF Core's Abstract Factory hook, <see cref="IDesignTimeDbContextFactory{TContext}"/>)
/// that lets the `dotnet ef migrations` CLI build a <see cref="VendorDbContext"/> without going
/// through the API host's DI container, which the CLI tooling never runs.
/// </summary>
public sealed class VendorDbContextFactory : IDesignTimeDbContextFactory<VendorDbContext>
{
    public VendorDbContext CreateDbContext(string[] args)
    {
        // No DI container at design time, so the CLI can't read appsettings/user-secrets the way
        // the running API does; ConnectionStrings__Postgres (same variable docker-compose sets for
        // the API container) is the one override point, falling back to the local default that
        // matches .env.example's placeholder credentials.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<VendorDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "VNDR"));

        return new VendorDbContext(optionsBuilder.Options);
    }
}
