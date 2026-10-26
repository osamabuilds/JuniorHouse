using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Warehouse.Application.Putaway;
using Romp.Modules.Warehouse.Application.StockAlerts;
using Romp.Modules.Warehouse.Infrastructure.Putaway;
using Romp.Modules.Warehouse.Infrastructure.StockAlerts;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.CycleCounts;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Infrastructure.Adjustments;
using Romp.Modules.Warehouse.Infrastructure.Bins;
using Romp.Modules.Warehouse.Infrastructure.CycleCounts;
using Romp.Modules.Warehouse.Infrastructure.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure;

/// <summary>
/// The <c>WHSE</c> module's entry point (Warehouse, Sprint 6): bins, putaway by scan, the stock ledger, reason-coded adjustments,
/// cycle counts and low-stock alerts. Stock becomes available only after putaway (FR-SC-08). It reads other modules only through
/// their <c>*.Contracts</c> and reacts to QC's and Labelling's events (ADR 0002, ADR 0011, ADR 0012).
/// </summary>
public sealed class WarehouseModule : IModule
{
    public string Name => "Warehouse";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WhseDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "WHSE"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));
        services.AddScoped<IWarehouseDbContext>(sp => sp.GetRequiredService<WhseDbContext>());

        // "Configuration, not constants" (D5, D13): validated when the host starts.
        services.AddOptions<WarehouseOptions>().Bind(configuration.GetSection("Warehouse")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<WarehouseOptions>, WarehouseOptionsValidator>();

        // Each feature's public surface, implemented by its own feature and used through the interface (one-way dependencies).
        services.AddScoped<IBinReader, BinReader>();
        services.AddScoped<IBinCountMarker, BinCountMarker>();
        services.AddScoped<IStockPosting, StockPosting>();
        services.AddScoped<IAdjustmentPoster, AdjustmentPoster>();
        services.AddScoped<ICountNumberAllocator, CountNumberAllocator>();
        services.AddScoped<AlertEvaluator>();
        services.AddScoped<IStockVariantReader, StockVariantReader>();
        services.AddScoped<Romp.Modules.Warehouse.Contracts.IStockAvailabilityQueries, Romp.Modules.Warehouse.Application.StockLedger.StockAvailabilityQueries>();
        services.AddScoped<IBinStockQuery, BinStockQuery>(); // bins declares the port, the ledger answers it

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(WarehouseTransactionBehavior<,>));

        // Warehouse's events go to the shared dispatcher; any event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule("WHSE", typeof(StockPutawayConfirmedEvent), typeof(StockLevelChangedEvent), typeof(StockAlertRaisedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        // Warehouse's own consumers of Labelling's variant events (the variant reference, D3). The ledger's consumer is registered first
        // so the reference exists before putaway links its waiting items.
        services.AddScoped<IOutboxMessageHandler<VariantGtinAssignedEvent>, VariantGtinAssignedConsumer>();
        services.AddScoped<IOutboxMessageHandler<VariantGtinAssignedEvent>, VariantGtinAssignedPutawayConsumer>();
        services.AddScoped<IOutboxMessageHandler<GtinVoidedEvent>, GtinVoidedConsumer>();

        // Putaway: QC's accepted batches become items waiting to be shelved (FR-SC-08).
        services.AddScoped<IPoStyleLookup, ContractsPoStyleLookup>();
        services.AddScoped<IPutawayRowLocks, PutawayRowLocks>();
        services.AddScoped<IOutboxMessageHandler<QcBatchAcceptedEvent>, QcBatchAcceptedConsumer>();

        // Alerts react to the module's own stock events, in their own transaction (ALR-9).
        services.AddScoped<IOutboxMessageHandler<StockLevelChangedEvent>, StockLevelChangedAlertConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapBinEndpoints();
        endpoints.MapStockEndpoints();
        endpoints.MapPutawayEndpoints();
        endpoints.MapAdjustmentEndpoints();
        endpoints.MapCycleCountEndpoints();
        endpoints.MapStockAlertEndpoints();
    }
}
