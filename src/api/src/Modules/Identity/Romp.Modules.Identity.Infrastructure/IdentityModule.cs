using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Identity.Infrastructure.Persistence;

namespace Romp.Modules.Identity.Infrastructure;

/// <summary>
/// The <c>IDNT</c> module's entry point (Identity, Sprint 8, ADR 0015): staff accounts, sign-in, MFA, roles and the audit trail.
/// Sprint 8 task G2 scaffolds it; each feature registers its own services and endpoints as it lands.
/// </summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdntDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "IDNT"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));

        // The module's events (none yet) go to the shared dispatcher; each feature adds its own as it lands.
        services.AddOutboxModule("IDNT");
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
