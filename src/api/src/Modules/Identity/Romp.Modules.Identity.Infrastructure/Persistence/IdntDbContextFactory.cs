using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Romp.Modules.Identity.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build an <see cref="IdntDbContext"/> without the API host's DI container.</summary>
public sealed class IdntDbContextFactory : IDesignTimeDbContextFactory<IdntDbContext>
{
    public IdntDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<IdntDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "IDNT"));

        return new IdntDbContext(optionsBuilder.Options);
    }
}
