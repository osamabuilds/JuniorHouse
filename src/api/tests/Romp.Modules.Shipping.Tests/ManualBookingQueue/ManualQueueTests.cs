using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.ManualBookingQueue;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.ManualBookingQueue;

/// <summary>S14 S5: a parcel no courier would take waits, is retried, and can take a hand-entered waybill (MBQ-1 to MBQ-8).</summary>
[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class ManualQueueTests(ShippingPostgres postgres)
{
    private static async Task<long> StuckAsync(ShippingHost host, string address = "House 4")
    {
        var id = await host.NewShipmentAsync(address: address);
        host.CourierState.Outage[Couriers.Primary] = true;
        host.CourierState.Outage[Couriers.Secondary] = true;
        await host.SendAsync(new BookShipmentCommand(id));
        host.CourierState.Outage[Couriers.Primary] = false;
        host.CourierState.Outage[Couriers.Secondary] = false;
        return id;
    }

    [Fact]
    [Trait("Spec", "MBQ-1")]
    public async Task TheQueue_ListsPendingShipments_OldestFirst_WithReasonAttemptsAndWaitingTime_Paged()
    {
        using var host = new ShippingHost(postgres);
        var first = await StuckAsync(host);
        host.Time.Now = ShippingHost.Start.AddMinutes(30);
        var second = await StuckAsync(host);
        var booked = await host.NewBookedShipmentAsync();
        host.Time.Now = ShippingHost.Start.AddMinutes(90);

        var page = await host.SendAsync(new ListManualQueueQuery(false, 1, 100));

        var rows = page.Items.Where(r => r.Id == first || r.Id == second).ToList();
        Assert.Equal([first, second], rows.Select(r => r.Id));
        Assert.Equal(("UNREACHABLE", 1, false), (rows[0].Reason, rows[0].Attempts, rows[0].NeedsStaff));
        Assert.True(rows[0].WaitingMinutes >= rows[1].WaitingMinutes);
        Assert.DoesNotContain(page.Items, r => r.Id == booked);
        Assert.Equal(1, (await host.SendAsync(new ListManualQueueQuery(false, 1, 1))).PageSize == 1 ? 1 : 0);
    }

    [Fact]
    [Trait("Spec", "MBQ-2")]
    public async Task WhenTheRetryTimeComes_TheWorkerBooksIt_NotBefore()
    {
        using var host = new ShippingHost(postgres);
        var id = await StuckAsync(host);

        var early = await host.SendAsync(new BookDueShipmentsCommand(100));
        host.Time.Now = ShippingHost.Start.AddMinutes(host.Options.RetryMinutes + 1);
        await host.SendAsync(new BookDueShipmentsCommand(100));

        Assert.Equal(0, early);
        Assert.Equal(ShipmentStatuses.Booked, (await ShippingHost.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "MBQ-6")]
    public async Task WhenTheAttemptsAreUsedUp_TheShipmentWaitsForStaff_AndNeedsStaffOnlyFiltersIt()
    {
        using var host = new ShippingHost(postgres, o => o.MaxAttempts = 2);
        var id = await host.NewShipmentAsync();
        host.CourierState.Outage[Couriers.Primary] = true;
        host.CourierState.Outage[Couriers.Secondary] = true;
        await host.SendAsync(new BookShipmentCommand(id));
        host.Time.Now = ShippingHost.Start.AddMinutes(10);
        await host.SendAsync(new BookShipmentCommand(id));

        var shipment = await ShippingHost.LoadAsync(postgres, id);
        var staffOnly = await host.SendAsync(new ListManualQueueQuery(true, 1, 100));

        Assert.True(shipment.NeedsStaff);
        Assert.Null(shipment.NextAttemptAt);
        Assert.Contains(staffOnly.Items, r => r.Id == id && r.NeedsStaff);
        host.Time.Now = ShippingHost.Start.AddHours(5);
        Assert.Equal(0, await host.SendAsync(new BookDueShipmentsCommand(100)));
    }

    [Fact]
    [Trait("Spec", "MBQ-3")]
    public async Task TryAgainNow_BooksOnce_AndASecondPressFindsItDone()
    {
        using var host = new ShippingHost(postgres);
        var id = await StuckAsync(host);

        Assert.True(await host.SendAsync(new RetryNowCommand(id, ShippingHost.Staff)));
        var first = await host.SendAsync(new BookShipmentCommand(id));
        Assert.False(await host.SendAsync(new RetryNowCommand(id, ShippingHost.Staff)));
        var second = await host.SendAsync(new BookShipmentCommand(id));

        Assert.Equal(BookOutcome.Booked, first);
        Assert.NotEqual(BookOutcome.Booked, second);
        Assert.False(await host.SendAsync(new RetryNowCommand(404_404, ShippingHost.Staff)));
    }

    [Fact]
    [Trait("Spec", "MBQ-4")]
    public async Task AWaybillFromThePhone_BooksTheShipment_WithTheSourceRecordedAsManual()
    {
        using var host = new ShippingHost(postgres);
        var id = await StuckAsync(host);

        await host.SendAsync(new EnterManualWaybillCommand(id, "secondary", "ph-12345", ShippingHost.Staff));

        var shipment = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal(("PH-12345", Couriers.Secondary, BookingSources.Manual, ShipmentStatuses.Booked), (shipment.Waybill, shipment.CourierId, shipment.BookingSourceId, shipment.StatusId));
    }

    [Fact]
    [Trait("Spec", "MBQ-5")]
    public async Task AWaybillAnotherParcelHolds_IsRefused()
    {
        using var host = new ShippingHost(postgres);
        var first = await StuckAsync(host);
        var second = await StuckAsync(host);
        await host.SendAsync(new EnterManualWaybillCommand(first, "PRIMARY", "DUP-1", ShippingHost.Staff));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new EnterManualWaybillCommand(second, "PRIMARY", "dup-1", ShippingHost.Staff)));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new EnterManualWaybillCommand(second, "NOBODY", "OK-1", ShippingHost.Staff)));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new EnterManualWaybillCommand(first, "PRIMARY", "OTHER-1", ShippingHost.Staff)));
    }

    [Fact]
    [Trait("Spec", "MBQ-8")]
    public async Task ARefusedParcel_IsNotRetriedAutomatically_ButStaffCanTryAgain()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync(address: "REJECT road");
        await host.SendAsync(new BookShipmentCommand(id));
        host.Time.Now = ShippingHost.Start.AddHours(3);

        var worker = await host.SendAsync(new BookDueShipmentsCommand(100));
        await host.SendAsync(new RetryNowCommand(id, ShippingHost.Staff));
        var staff = await host.SendAsync(new BookShipmentCommand(id));

        Assert.Equal(0, worker);
        Assert.Equal(BookOutcome.Rejected, staff);
    }

    [Fact]
    [Trait("Spec", "MBQ-7")]
    public async Task EveryQueueAction_NeedsASignedInStaffMember()
    {
        using var host = new ShippingHost(postgres);
        var id = await StuckAsync(host);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RetryNowCommand(id, " ")));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new EnterManualWaybillCommand(id, "PRIMARY", "X-1", "")));
    }
}
