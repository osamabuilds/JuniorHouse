using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Catalog.Infrastructure.Styles;

namespace Romp.Modules.Catalog.Infrastructure;

/// <summary>
/// The <c>CTLG</c> module's entry point (see <see cref="IModule"/> - each module is a Strategy the
/// host iterates over, so adding a module never means editing another module's code).
/// </summary>
public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CTLG"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IStyleQueries, StyleQueries>();

        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CatalogTransactionBehavior<,>));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapStyleEndpoints(Name);
}
