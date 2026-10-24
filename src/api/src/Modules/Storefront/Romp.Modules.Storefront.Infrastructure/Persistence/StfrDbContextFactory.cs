using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Romp.Modules.Storefront.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build a <see cref="StfrDbContext"/> without the API host's DI container.</summary>
public sealed class StfrDbContextFactory : IDesignTimeDbContextFactory<StfrDbContext>
{
    public StfrDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<StfrDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"));

        return new StfrDbContext(optionsBuilder.Options);
    }
}
