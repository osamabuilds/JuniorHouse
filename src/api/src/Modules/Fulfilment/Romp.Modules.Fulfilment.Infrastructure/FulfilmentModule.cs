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
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Application.PackStation;
using Romp.Modules.Fulfilment.Application.PackingSlips;
using Romp.Modules.Fulfilment.Contracts;
using Romp.Modules.Fulfilment.Infrastructure.Allocations;
using Romp.Modules.Fulfilment.Infrastructure.PackStation;
using Romp.Modules.Fulfilment.Infrastructure.PackingSlips;
using Romp.Modules.Fulfilment.Infrastructure.Persistence;
using Romp.Modules.Fulfilment.Infrastructure.PickExceptions;
using Romp.Modules.Fulfilment.Infrastructure.PickLists;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Fulfilment.Infrastructure;

/// <summary>
/// The <c>FULF</c> module's entry point (Fulfilment, Sprint 14): allocations, pick lists, pick exceptions, the pack station and packing slips.
/// It starts when an order is confirmed or paid, reads Orders and Warehouse only through their <c>*.Contracts</c>, and publishes events that carry
/// ids, the order number and codes only (ADR 0002, ADR 0011, ADR 0030).
/// </summary>
public sealed class FulfilmentModule : IModule
{
    public string Name => "Fulfilment";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FulfDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "FULF"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));
        services.AddScoped<IFulfilmentDbContext>(sp => sp.GetRequiredService<FulfDbContext>());

        // "Configuration, not constants" (ADR 0030 point 6): validated when the host starts.
        services.AddOptions<FulfilmentOptions>().Bind(configuration.GetSection("Fulfilment")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<FulfilmentOptions>, FulfilmentOptionsValidator>();

        services.AddScoped<PackPlanner>();
        services.AddScoped<PackingSlipBuilder>();
        services.TryAddSingleton<ISlipPrinter, LoggingSlipPrinter>();

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(FulfilmentTransactionBehavior<,>));

        services.AddOutboxModule("FULF", typeof(PickCompletedEvent), typeof(OrderLineDelayedEvent), typeof(PickExceptionRaisedEvent), typeof(OrderPackedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        // Orders' events start and stop fulfilment (ALC-1, ALC-2, ALC-6), each behind the inbox.
        services.AddScoped<IOutboxMessageHandler<OrderConfirmedEvent>, OrderConfirmedFulfilmentConsumer>();
        services.AddScoped<IOutboxMessageHandler<OrderPaidEvent>, OrderPaidFulfilmentConsumer>();
        services.AddScoped<IOutboxMessageHandler<OrderCancelledEvent>, OrderCancelledFulfilmentConsumer>();

        services.AddHostedService<FulfilmentMaintenanceWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPickListEndpoints();
        endpoints.MapPickExceptionEndpoints();
        endpoints.MapPackStationEndpoints();
        endpoints.MapPackingSlipEndpoints();
    }
}
