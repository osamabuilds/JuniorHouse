using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Romp.Modules.Labelling.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build a <see cref="SkuDbContext"/> without the API host's DI container.</summary>
public sealed class SkuDbContextFactory : IDesignTimeDbContextFactory<SkuDbContext>
{
    public SkuDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<SkuDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SKU"));

        return new SkuDbContext(optionsBuilder.Options);
    }
}
