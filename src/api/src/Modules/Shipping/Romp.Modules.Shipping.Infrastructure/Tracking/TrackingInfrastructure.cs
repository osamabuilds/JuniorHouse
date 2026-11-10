using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Fulfilment.Contracts;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.Manifests;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Infrastructure.Persistence;

namespace Romp.Modules.Shipping.Infrastructure.Tracking;

/// <summary>
/// What Orders and the public tracking page may read about a shipment (ADR 0002): the courier, the waybill, the current status and the timeline, and
/// nothing else (OHS-5, TRK-9). The timeline is the booking and every courier status that moved the parcel forward; an older status that arrived late
/// is in staff's history but not shown to the customer.
/// </summary>
internal sealed class ShipmentTrackingQueries(ShpgDbContext dbContext) : IShipmentTrackingQueries
{
    public Task<ShipmentTrackingView?> FindByOrderAsync(long orderId, CancellationToken cancellationToken) =>
        FindAsync(dbContext.Shipments.AsNoTracking().Where(s => s.OrderId == orderId), cancellationToken);

    public Task<ShipmentTrackingView?> FindByWaybillAsync(string waybill, CancellationToken cancellationToken) =>
        FindAsync(dbContext.Shipments.AsNoTracking().Where(s => s.Waybill == waybill), cancellationToken);

    private static async Task<ShipmentTrackingView?> FindAsync(IQueryable<Domain.Bookings.Shipment> query, CancellationToken cancellationToken)
    {
        var shipment = await query.Include(s => s.Events).AsSplitQuery().FirstOrDefaultAsync(cancellationToken);
        if (shipment?.Waybill is null || shipment.CourierId is null || shipment.StatusId == ShipmentStatuses.Cancelled)
        {
            return null;
        }

        var steps = new List<TrackingStep> { new(ShipmentStatusCodes.Booked, ShipmentStatuses.NameOf(ShipmentStatuses.Booked), shipment.BookedAt ?? shipment.StatusAt) };
        var rank = ShipmentStatuses.RankOf(ShipmentStatuses.Booked);
        foreach (var e in shipment.Events.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id))
        {
            var r = ShipmentStatuses.RankOf(e.StatusId);
            if (r >= rank && !(steps[^1].StatusCode == ShipmentStatuses.CodeOf(e.StatusId)))
            {
                steps.Add(new TrackingStep(ShipmentStatuses.CodeOf(e.StatusId), ShipmentStatuses.NameOf(e.StatusId), e.OccurredAt));
                rank = r;
            }
        }

        return new ShipmentTrackingView(Couriers.NameOf(shipment.CourierId.Value), shipment.Waybill, ShipmentStatuses.CodeOf(shipment.StatusId), ShipmentStatuses.NameOf(shipment.StatusId), steps);
    }
}

/// <summary>
/// Failed public tracking look-ups per client address in a sliding ten minutes, kept in memory (SEC-06). After <c>Shipping:TrackingFailuresBeforeCaptcha</c>
/// the address must pass a CAPTCHA. The count resets when the process restarts, which only makes a restart briefly more permissive; the endpoint is
/// also rate limited, and a shared store can replace this without changing the port.
/// </summary>
internal sealed class TrackingThrottle(IMemoryCache cache, IOptions<ShippingOptions> options) : ITrackingThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public bool NeedsCaptcha(string? clientIp) => cache.TryGetValue(Key(clientIp), out int failures) && failures >= options.Value.TrackingFailuresBeforeCaptcha;

    public void RecordFailure(string? clientIp)
    {
        var key = Key(clientIp);
        cache.Set(key, (cache.TryGetValue(key, out int n) ? n : 0) + 1, Window);
    }

    private static string Key(string? clientIp) => "trk-fail:" + (clientIp ?? "unknown");
}

// The one place other modules' events become Shipping commands (the anti-corruption mapper, ADR 0025). Each sits behind the inbox, so a redelivered
// message is a no-op (BKG-5).

/// <summary>A parcel was packed and weighed: its shipment is created and booked at once (BKG-1). The booking runs here, in the outbox worker, never in the pack request.</summary>
internal sealed class OrderPackedShippingConsumer(ISender sender, ShpgDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderPackedEvent>
{
    public async Task HandleAsync(OrderPackedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (!await guard.TryBeginAsync(dbContext, "CreateShipmentOnOrderPacked", cancellationToken))
        {
            return;
        }

        var created = await sender.Send(new CreateShipmentCommand(domainEvent.OrderId, domainEvent.OrderNumber, domainEvent.WeightGrams), cancellationToken);
        if (created.ShipmentId is { } id)
        {
            // The booking claims the shipment with a conditional update, so nothing tracked from the creation may stay in this context.
            dbContext.ChangeTracker.Clear();
            await sender.Send(new BookShipmentCommand(id), cancellationToken);
        }
    }
}

/// <summary>An order was cancelled: an unbooked or uncollected shipment is cancelled too (BKG-10).</summary>
internal sealed class OrderCancelledShippingConsumer(ISender sender, ShpgDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderCancelledEvent>
{
    public async Task HandleAsync(OrderCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CancelShipmentOnOrderCancelled", cancellationToken))
        {
            await sender.Send(new CancelShipmentCommand(domainEvent.OrderId), cancellationToken);
        }
    }
}

/// <summary>
/// Background upkeep, one pass a minute: books the shipments whose time has come (MBQ-2), polls the couriers that do not push (TRK-6), and at the
/// business hour makes the day's manifests (MNF-2). A failure in one pass is logged and the next pass tries again; it never stops the host.
/// </summary>
public sealed partial class ShippingMaintenanceWorker(IServiceScopeFactory scopes, IOptions<ShippingOptions> options, TimeProvider clock, ILogger<ShippingMaintenanceWorker> logger) : BackgroundService
{
    private DateOnly _lastManifestDay = DateOnly.MinValue;
    private DateOnly _lastCodFlagDay = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new BookDueShipmentsCommand(), stoppingToken);
                await sender.Send(new PollShipmentsCommand(), stoppingToken);
                var now = clock.GetUtcNow();
                var today = BusinessDay.Of(now);
                if (now.ToOffset(TimeSpan.FromHours(5)).Hour >= options.Value.ManifestHour && today != _lastManifestDay)
                {
                    await sender.Send(new GenerateManifestsCommand(), stoppingToken);
                    _lastManifestDay = today;
                }

                // Once a business day: flag delivered cash parcels still unpaid after the courier cycle (COD-11).
                if (today != _lastCodFlagDay)
                {
                    await sender.Send(new Romp.Modules.Shipping.Application.CodReconciliation.FlagOverdueCodCommand(), stoppingToken);
                    _lastCodFlagDay = today;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(ex);
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shipping upkeep failed; it will try again.")]
    private partial void LogFailed(Exception exception);
}
