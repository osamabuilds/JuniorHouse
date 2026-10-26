using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Infrastructure.Deliveries;
using Romp.Modules.Production.Infrastructure.Milestones;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Production.Infrastructure.ProductionRuns;
using Romp.Modules.Production.Infrastructure.Samples;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Infrastructure;

/// <summary>
/// The <c>PROD</c> module's entry point (Production Tracking, Sprint 3): the milestone timeline, the
/// PP-sample gate and vendor delivery notes for each acknowledged purchase order. It reads the PO
/// only through <c>Vendor.Contracts</c> and reacts to VNDR's events (ADR 0002, ADR 0008).
/// </summary>
public sealed class ProductionModule : IModule
{
    public string Name => "Production";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProductionDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "PROD"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));

        services.AddScoped<IProductionDbContext>(sp => sp.GetRequiredService<ProductionDbContext>());
        services.AddScoped<IProductionQueries, ProductionQueries>();

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ProductionTransactionBehavior<,>));

        // "Configuration, not constants": recorded-late days (D15), at-risk buffer (D18), PP file limits (D7).
        var defaults = new ProductionOptions();
        services.AddSingleton(new ProductionOptions
        {
            RecordedLateDays = configuration.GetValue("Prod:RecordedLateDays", defaults.RecordedLateDays),
            AtRiskBufferDays = configuration.GetValue("Prod:AtRiskBufferDays", defaults.AtRiskBufferDays),
            MaxFileSizeBytes = configuration.GetValue("Prod:PpFiles:MaxFileSizeBytes", defaults.MaxFileSizeBytes),
            MaxFilesPerRound = configuration.GetValue("Prod:PpFiles:MaxFilesPerRound", defaults.MaxFilesPerRound),
        });

        // Shared file storage (ADR 0008): the same root as PO files, keys are generated so modules never collide.
        services.AddFileStorage(configuration);

        // PROD's events go to the shared dispatcher; every event without a real consumer of its own
        // gets the placeholder logging handler (registered once for all modules).
        services.AddOutboxModule(
            "PROD",
            typeof(ProductionRunOpenedEvent),
            typeof(ProductionMilestoneReachedEvent),
            typeof(ProductionStartedEvent),
            typeof(DeliveryNoteRecordedEvent),
            typeof(ProductionRunCancelledEvent),
            typeof(ProductionRunClosedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        // PROD's own consumers of VNDR's events (SCRUM-185).
        services.AddScoped<IOutboxMessageHandler<PoAcknowledgedEvent>, PoAcknowledgedConsumer>();
        services.AddScoped<IOutboxMessageHandler<PoCancelledEvent>, PoCancelledConsumer>();
        services.AddScoped<IOutboxMessageHandler<PoClosedEvent>, PoClosedConsumer>();
        services.AddScoped<IOutboxMessageHandler<PoRevisionPutInForceEvent>, PoRevisionPutInForceConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProductionRunEndpoints();
        endpoints.MapMilestoneEndpoints();
        endpoints.MapPpSampleEndpoints();
        endpoints.MapDeliveryNoteEndpoints();
    }
}
