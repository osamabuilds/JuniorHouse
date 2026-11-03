using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Infrastructure;

/// <summary>
/// The <c>NTFY</c> module's entry point (Notifications, Sprint 13): guaranteed customer messages by email and WhatsApp, delivered from the
/// outbox with retry, dead-lettering and fallback (ADR 0026). It reads Orders only through <c>Orders.Contracts</c> and stores no message
/// body, address or phone.
/// </summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NtfyDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "NTFY"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));

        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NtfyDbContext>());
        services.TryAddSingleton(TimeProvider.System);

        services.AddOutboxModule("NTFY");
        services.AddScoped<CreateNotification>();

        // The delivery worker: claim, render, send, record (V4). Email goes through the shared port; WhatsApp joins with the channels feature.
        services.AddEmail(configuration);
        services.AddOptions<DeliverySettings>().Bind(configuration.GetSection(DeliverySettings.SectionName))
            .Validate(o => o.IsValid, "Notifications settings are invalid: the retry schedule needs at least 5 attempts.")
            .ValidateOnStart();
        services.AddScoped(sp => sp.GetRequiredService<IOptions<DeliverySettings>>().Value);
        services.AddScoped<IDeliveryClaimer, SkipLockedDeliveryClaimer>();
        services.AddScoped<IMessageRenderer, PlainMessageRenderer>();
        services.AddScoped<IChannelSender, EmailChannelSender>();
        services.AddScoped<DeliveryProcessor>();
        services.AddHostedService<DeliveryWorker>();

        // Orders' events become notifications through the one mapper (ADR 0026).
        services.AddScoped<IOutboxMessageHandler<OrderPlacedEvent>, OrderPlacedNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<OrderPaidEvent>, OrderPaidNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<OrderCancelledEvent>, OrderCancelledNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<CodConfirmationRequestedEvent>, CodConfirmationRequestedNotificationConsumer>();
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
