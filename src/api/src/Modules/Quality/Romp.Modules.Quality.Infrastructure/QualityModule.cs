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
using Romp.Modules.Production.Contracts;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Infrastructure.Batches;
using Romp.Modules.Quality.Infrastructure.InboundDeliveries;
using Romp.Modules.Quality.Infrastructure.Inspections;
using Romp.Modules.Quality.Infrastructure.Outcomes;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Quality.Infrastructure.Receiving;
using Romp.Modules.Quality.Infrastructure.SafetyChecks;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Quality.Infrastructure;

/// <summary>
/// The <c>QC</c> module's entry point (Receiving and Quality Control, Sprint 4): goods receipt against a delivery note,
/// QC by batch (AQL sampling, child-safety checks, outcomes) and the release of accepted quantity. It reads other modules
/// only through their <c>*.Contracts</c> and reacts to their events (ADR 0002, ADR 0008).
/// </summary>
public sealed class QualityModule : IModule
{
    public string Name => "Quality";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QcDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "QC"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));

        services.AddScoped<IQualityDbContext>(sp => sp.GetRequiredService<QcDbContext>());
        services.AddScoped<IQualityQueries, QualityQueries>();
        services.AddScoped<IGrnNumberAllocator, GrnNumberAllocator>();

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(QcTransactionBehavior<,>));

        // "Configuration, not constants" (spec Q1, AC-46).
        var defaults = new QualityOptions();
        services.AddSingleton(new QualityOptions
        {
            FullInspectionMaxLot = configuration.GetValue("Qc:FullInspectionMaxLot", defaults.FullInspectionMaxLot),
            DefaultInspectionLevelCode = configuration.GetValue("Qc:DefaultInspectionLevelCode", defaults.DefaultInspectionLevelCode)!,
            DefaultAqlLevelCode = configuration.GetValue("Qc:DefaultAqlLevelCode", defaults.DefaultAqlLevelCode)!,
            CriticalDefectTolerance = configuration.GetValue("Qc:CriticalDefectTolerance", defaults.CriticalDefectTolerance),
            ReceiptFutureDays = configuration.GetValue("Qc:ReceiptFutureDays", defaults.ReceiptFutureDays),
        });

        // QC's events go to the shared dispatcher; every event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule(
            "QC",
            typeof(GoodsReceiptRecordedEvent),
            typeof(QcBatchAcceptedEvent),
            typeof(QcBatchRejectedEvent),
            typeof(QcBatchReworkRequestedEvent),
            typeof(QcInspectionRecordedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        // QC's own consumers of PROD's and VNDR's events.
        services.AddScoped<IOutboxMessageHandler<DeliveryNoteRecordedEvent>, DeliveryNoteRecordedConsumer>();
        services.AddScoped<IOutboxMessageHandler<PoCancelledEvent>, PoCancelledInboundConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapInboundDeliveryEndpoints();
        endpoints.MapGoodsReceiptEndpoints();
        endpoints.MapQcBatchEndpoints();
        endpoints.MapInspectionEndpoints();
        endpoints.MapSafetyCheckEndpoints();
        endpoints.MapOutcomeEndpoints();
    }
}
