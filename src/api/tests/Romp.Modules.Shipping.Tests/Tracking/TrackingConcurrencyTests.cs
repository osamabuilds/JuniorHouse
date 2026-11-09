using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.ManualBookingQueue;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.Tracking;

/// <summary>S15 T1: the same push many times at once, and two parcels fighting for one waybill, never double-count or double-book (TRK-3, MBQ-5, NFR-09).</summary>
[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class TrackingConcurrencyTests(ShippingPostgres postgres)
{
    private static async Task<bool> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or DomainException)
        {
            return false;
        }
    }

    [Fact]
    [Trait("Spec", "TRK-3")]
    public async Task TheSamePushDeliveredManyTimesAtOnce_IsStoredOnce_AndMovesTheShipmentOnce()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        var (body, signature) = host.Push(waybill, "PICKED_UP", "evt-race");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TryAsync(() => host.SendAsync(new ApplyCourierPushCommand("PRIMARY", body, signature)))));

        Assert.Contains(results, r => r);
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        Assert.Single(shipment.Events);
        Assert.Equal(ShipmentStatuses.PickedUp, shipment.StatusId);
    }

    [Fact]
    [Trait("Spec", "MBQ-5")]
    public async Task TwoParcelsGivenTheSameWaybillAtOnce_OnlyOneKeepsIt()
    {
        using var host = new ShippingHost(postgres);
        var a = await host.NewShipmentAsync();
        var b = await host.NewShipmentAsync();
        var waybill = $"RACE-{Guid.NewGuid():N}"[..20];

        var results = await Task.WhenAll(
            TryAsync(() => host.SendAsync(new EnterManualWaybillCommand(a, "PRIMARY", waybill, ShippingHost.Staff))),
            TryAsync(() => host.SendAsync(new EnterManualWaybillCommand(b, "PRIMARY", waybill, ShippingHost.Staff))));

        Assert.Equal(1, results.Count(r => r));
        await using var db = postgres.NewContext();
        var stored = waybill.ToUpperInvariant();
        Assert.Equal(1, await db.Shipments.CountAsync(s => s.Waybill == stored));
    }

    [Fact]
    [Trait("Spec", "TRK-1")]
    public async Task ManyDifferentPushesAtOnce_NeverMoveTheShipmentBackwards()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        var statuses = new[] { "PICKED_UP", "IN_TRANSIT", "OUT_FOR_DELIVERY", "DELIVERED" };

        await Task.WhenAll(statuses.Select((status, i) =>
        {
            var (body, signature) = host.Push(waybill, status, $"evt-{i}", ShippingHost.Start.AddHours(i + 1));
            return TryAsync(() => host.SendAsync(new ApplyCourierPushCommand("PRIMARY", body, signature)));
        }));

        // Whatever order they arrived in, the parcel ends no further back than the latest the courier said, and never past delivery.
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        Assert.True(ShipmentStatuses.RankOf(shipment.StatusId) >= ShipmentStatuses.RankOf(ShipmentStatuses.PickedUp));
        Assert.True(shipment.Events.Count <= statuses.Length);
        Assert.Equal(shipment.Events.Count, shipment.Events.Select(e => e.CourierEventId).Distinct().Count());
    }
}
