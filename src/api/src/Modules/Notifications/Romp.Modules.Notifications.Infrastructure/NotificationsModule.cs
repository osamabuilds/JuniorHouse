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
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Application.DeliveryLog;
using Romp.Modules.Notifications.Contracts;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Infrastructure.DeliveryLog;
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

        services.AddOutboxModule("NTFY", typeof(NotificationDeadLetteredEvent), typeof(NotificationSentEvent));
        services.AddScoped<CreateNotification>();

        // The delivery worker: claim, render, send, record (V4). Email goes through the shared port; WhatsApp joins with the channels feature.
        services.AddEmail(configuration);
        services.AddOptions<DeliverySettings>().Bind(configuration.GetSection(DeliverySettings.SectionName))
            .Validate(o => o.IsValid, "Notifications settings are invalid: the retry schedule needs at least 5 attempts.")
            .ValidateOnStart();
        services.AddScoped(sp => sp.GetRequiredService<IOptions<DeliverySettings>>().Value);
        services.AddScoped<IDeliveryClaimer, SkipLockedDeliveryClaimer>();
        services.AddScoped<IMessageRenderer, TemplateMessageRenderer>();

        // One circuit per channel: a failing provider fails fast and never blocks the other channel (CHN-4).
        services.AddSingleton(configuration.GetSection(ChannelCircuitOptions.SectionName).Get<ChannelCircuitOptions>() ?? new ChannelCircuitOptions());
        services.AddSingleton<ChannelCircuits>();
        services.AddScoped<IChannelSender>(sp => new CircuitBreakingChannelSender(ActivatorUtilities.CreateInstance<EmailChannelSender>(sp), sp.GetRequiredService<ChannelCircuits>()));

        // WhatsApp sits behind its port. Off by default, the fake stands in (CHN-8, D-4); with Enabled the Cloud API adapter takes over, and the
        // host refuses to start without its secrets outside Development (WhatsAppStartupCheck).
        var whatsAppSettings = configuration.GetSection(WhatsAppSettings.SectionName).Get<WhatsAppSettings>() ?? new WhatsAppSettings();
        services.AddSingleton(whatsAppSettings);
        services.AddHostedService<WhatsAppStartupCheck>();
        if (whatsAppSettings.Enabled)
        {
            services.AddHttpClient<WhatsAppCloudSender>(client =>
            {
                client.BaseAddress = new Uri(whatsAppSettings.ApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(whatsAppSettings.TimeoutSeconds);
            });
            services.AddTransient<IWhatsAppSender>(sp => sp.GetRequiredService<WhatsAppCloudSender>());
        }
        else
        {
            services.TryAddSingleton<FakeWhatsAppSender>();
            services.TryAddSingleton<IWhatsAppSender>(sp => sp.GetRequiredService<FakeWhatsAppSender>());
        }

        services.AddScoped<IChannelSender>(sp => new CircuitBreakingChannelSender(ActivatorUtilities.CreateInstance<WhatsAppChannelSender>(sp), sp.GetRequiredService<ChannelCircuits>()));

        // Inbound WhatsApp: signature-checked webhook, events stored once, routed by type (CHN-11).
        services.AddScoped<WhatsAppWebhookProcessor>();
        services.AddScoped<ISuppressionList, SuppressionList>();
        services.AddScoped<IDailySendCounter, SqlDailySendCounter>();
        services.AddScoped<IWhatsAppInboundHandler, ReceiptHandler>();
        services.AddScoped<IWhatsAppInboundHandler, CodReplyHandler>();
        services.AddScoped<IWhatsAppInboundHandler, StopHandler>();
        services.AddScoped<DeliveryProcessor>();
        services.AddScoped<QueueHealth>();
        services.AddScoped<DeliveryLogQueries>();
        services.AddScoped<ResendNotification>();
        services.AddScoped<PurgeProcessor>();
        services.AddScoped<IOutboxMessageHandler<NotificationDeadLetteredEvent>, DeadLetterAlertConsumer>();
        services.AddHealthChecks().AddCheck<QueueAgeHealthCheck>(QueueAgeHealthCheck.Name, tags: ["ready"]);
        services.AddHostedService<DeliveryWorker>();
        services.AddHostedService<PurgeWorker>();

        // Orders' events become notifications through the one mapper (ADR 0026).
        services.AddScoped<IOutboxMessageHandler<OrderPlacedEvent>, OrderPlacedNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<OrderPaidEvent>, OrderPaidNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<OrderCancelledEvent>, OrderCancelledNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<CodConfirmationRequestedEvent>, CodConfirmationRequestedNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Fulfilment.Contracts.OrderLineDelayedEvent>, OrderLineDelayedNotificationConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Shipping.Contracts.ShipmentStatusChangedEvent>, ShipmentStatusNotificationConsumer>();
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapWhatsAppWebhook();
        endpoints.MapDeliveryLogEndpoints();
    }
}
