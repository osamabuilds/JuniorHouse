using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Reference.Application;
using Romp.Modules.Reference.Application.Abstractions;
using Romp.Modules.Reference.Infrastructure;
using Romp.Modules.Reference.Infrastructure.Persistence;

namespace Romp.Modules.Reference.Tests.Support;

/// <summary>
/// Builds the same MediatR + FluentValidation pipeline ReferenceModule wires in production, but
/// against EF's InMemory provider - appropriate here because these tests exercise our own command
/// wiring and business rules, not PostgreSQL-specific behaviour (CLAUDE.md reserves Testcontainers
/// for that; see RefMigrationTests for the one test in this project that needs real PostgreSQL).
/// </summary>
internal static class TestServices
{
    public static IServiceProvider Build(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<ReferenceDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IReferenceDbContext>(sp => sp.GetRequiredService<ReferenceDbContext>());

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ReferenceTransactionBehavior<,>));
        });
        ReferenceModule.RegisterLookupHandlers(services);

        return services.BuildServiceProvider();
    }
}
