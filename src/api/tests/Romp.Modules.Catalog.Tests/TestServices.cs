using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Infrastructure;

namespace Romp.Modules.Catalog.Tests;

/// <summary>
/// Builds the same MediatR + FluentValidation pipeline CatalogModule wires in production, but
/// against EF's InMemory provider (appropriate for our own command/validation logic, not
/// PostgreSQL-specific behaviour - CLAUDE.md reserves Testcontainers for that; see
/// CtlgMigrationTests for the one test here that needs real PostgreSQL).
/// </summary>
internal static class TestServices
{
    public static IServiceProvider Build(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CatalogTransactionBehavior<,>));
        });

        return services.BuildServiceProvider();
    }
}
