using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Romp.Modules.Customers.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build a <see cref="CustDbContext"/> without the API host's DI container.</summary>
public sealed class CustDbContextFactory : IDesignTimeDbContextFactory<CustDbContext>
{
    public CustDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<CustDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CUST"));

        return new CustDbContext(optionsBuilder.Options);
    }
}
