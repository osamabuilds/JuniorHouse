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
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Infrastructure.Checkout;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure;

/// <summary>
/// The <c>ORDR</c> module's entry point (Orders, Sprints 12 and 13): placing orders, payments, cash on delivery confirmation, the staff queue
/// and customer look-up (ADR 0024, 0025). It reads other modules only through their contracts and talks to them through events.
/// </summary>
public sealed class OrdersModule : IModule
{
    public string Name => "Orders";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrdrDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "ORDR"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));

        services.AddScoped<IOrdersDbContext>(sp => sp.GetRequiredService<OrdrDbContext>());
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOrderNumberAllocator, OrderNumberAllocator>();
        services.AddOptions<OrdersOptions>().Bind(configuration.GetSection(OrdersOptions.SectionName))
            .Validate(OrdersOptions.IsValid, "Orders:EnabledOnlineMethods may only contain EASY, JAZZ or CARD.")
            .ValidateOnStart();
        services.AddHttpContextAccessor();
        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OrdersTransactionBehavior<,>));

        // Orders' events go to the shared dispatcher; every event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule(
            "ORDR",
            typeof(Contracts.OrderPlacedEvent), typeof(Contracts.OrderPaidEvent), typeof(Contracts.OrderCancelledEvent),
            typeof(Contracts.CodConfirmationRequestedEvent), typeof(Contracts.OrderConfirmedEvent), typeof(Contracts.OrderPaymentSuspectEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCheckoutEndpoints();
    }
}
