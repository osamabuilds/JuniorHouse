using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure;

/// <summary>
/// The <c>STFR</c> module's entry point (Storefront, Sprint 9): admin-managed storefront settings, content pages and the published
/// snapshot the public site reads (ADR 0020). It reads other modules only through their <c>*.Contracts</c> (ADR 0002, ADR 0011).
/// </summary>
public sealed class StorefrontModule : IModule
{
    public string Name => "Storefront";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StfrDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<IStorefrontDbContext>(sp => sp.GetRequiredService<StfrDbContext>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Endpoints arrive with their features (tasks 11 and 12).
    }
}
