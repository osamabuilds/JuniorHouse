using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Fulfilment.Contracts;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Infrastructure.Bookings;
using Romp.Modules.Shipping.Infrastructure.CourierPort;
using Romp.Modules.Shipping.Infrastructure.Manifests;
using Romp.Modules.Shipping.Infrastructure.Persistence;
using Romp.Modules.Shipping.Infrastructure.Tracking;

namespace Romp.Modules.Shipping.Infrastructure;

/// <summary>
/// The <c>SHPG</c> module's entry point (Shipping, Sprint 15): bookings with a courier port, the manual booking queue, labels, manifests and tracking.
/// It starts when a parcel is packed, reads Orders only through its contract and talks to couriers only through <see cref="ICourierGateway"/>
/// (ADR 0002, ADR 0031). The pretend couriers exist only in Development and Testing.
/// </summary>
public sealed class ShippingModule : IModule
{
    public string Name => "Shipping";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ShpgDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SHPG"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));
        services.AddScoped<IShippingDbContext>(sp => sp.GetRequiredService<ShpgDbContext>());

        // "Configuration, not constants" (ADR 0031): validated when the host starts. In Development and Testing the two pretend couriers exist by default.
        services.AddOptions<ShippingOptions>().Bind(configuration.GetSection("Shipping")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ShippingOptions>, ShippingOptionsValidator>();
        services.AddOptions<ShippingOptions>().PostConfigure<IHostEnvironment>((o, environment) =>
        {
            if (!o.Couriers.ContainsKey("PRIMARY") && !o.Couriers.ContainsKey("SECONDARY") && RompAuthorizationExtensions.IsDevelopmentLike(environment))
            {
                o.Couriers["PRIMARY"] = new CourierSettings { WebhookSecret = "local-demo-courier-primary-secret-0123456789", PushesStatus = true };
                o.Couriers["SECONDARY"] = new CourierSettings { WebhookSecret = "local-demo-courier-secondary-secret-0123456789", PushesStatus = false };
            }
        });

        services.AddSingleton<FakeCourierState>();
        services.AddSingleton<ICourierRegistry>(sp => RompAuthorizationExtensions.IsDevelopmentLike(sp.GetRequiredService<IHostEnvironment>())
            ? new CourierRegistry(sp.GetRequiredService<IOptions<ShippingOptions>>(), sp.GetRequiredService<FakeCourierState>())
            : new EmptyCourierRegistry());

        services.AddMemoryCache();
        services.AddSingleton<ITrackingThrottle, TrackingThrottle>();
        services.AddScoped<ShipmentBooker>();
        services.AddScoped<IShipmentTrackingQueries, ShipmentTrackingQueries>();

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ShippingTransactionBehavior<,>));

        services.AddOutboxModule("SHPG", typeof(ShipmentBookedEvent), typeof(ShipmentStatusChangedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        services.AddScoped<IOutboxMessageHandler<OrderPackedEvent>, OrderPackedShippingConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Orders.Contracts.OrderCancelledEvent>, OrderCancelledShippingConsumer>();

        services.AddHostedService<ShippingMaintenanceWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapBookingEndpoints();
        endpoints.MapManifestEndpoints();
        endpoints.MapTrackingEndpoints();

        // The pretend couriers' controls: only where pretend couriers exist (like the pretend payment gateway). They let a demo or a test take a courier
        // "down" and make a courier push a status, signed with the courier's secret, through the real webhook code.
        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (RompAuthorizationExtensions.IsDevelopmentLike(environment))
        {
            var dev = endpoints.MapSecuredGroup("/dev/shipping", Policies.Public).WithTags("Shipping (development only)");
            dev.MapPost("/outage", (OutageRequest body, FakeCourierState state) =>
            {
                if (Couriers.FromCode(body.Courier) is not { } id)
                {
                    return Results.NotFound();
                }

                state.Outage[id] = body.Down;
                return Results.Ok(new { courier = body.Courier, down = body.Down });
            });
            dev.MapPost("/push", async (PretendPushRequest body, ICourierRegistry registry, IOptions<ShippingOptions> options, ISender sender, CancellationToken cancellationToken) =>
            {
                if (registry.FindByCode(body.Courier) is null || !options.Value.Couriers.TryGetValue(body.Courier!.ToUpperInvariant(), out var settings))
                {
                    return Results.NotFound();
                }

                var payload = JsonSerializer.Serialize(new { waybill = body.Waybill, status = body.Status, eventId = body.EventId ?? Guid.NewGuid().ToString("N"), at = DateTimeOffset.UtcNow });
                var outcome = await sender.Send(new ApplyCourierPushCommand(body.Courier!, payload, FakeCourierGateway.Sign(settings.WebhookSecret, payload)), cancellationToken);
                return Results.Ok(new { outcome = outcome.ToString() });
            });
        }
    }

}

public sealed record OutageRequest(string? Courier, bool Down);

public sealed record PretendPushRequest(string? Courier, string? Waybill, string? Status, string? EventId);

/// <summary>Outside Development and Testing no courier exists in this build: every booking goes to the manual queue until a real courier adapter is added (ADR 0031).</summary>
internal sealed class EmptyCourierRegistry : ICourierRegistry
{
    public IReadOnlyList<ICourierGateway> InOrder => [];

    public ICourierGateway? Find(short courierId) => null;

    public ICourierGateway? FindByCode(string? code) => null;
}
