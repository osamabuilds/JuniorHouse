using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.Bookings;

/// <summary>S14 S4, S10 and S12: a packed order is booked with the primary courier, the secondary takes over, nothing is booked twice (BKG-1 to BKG-11).</summary>
[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class BookingTests(ShippingPostgres postgres)
{
    private static async Task<List<string>> EventsAsync(ShippingPostgres postgres, string type, string orderNumber)
    {
        await using var db = postgres.NewContext();
        var payloads = await db.Set<OutboxMessage>().Where(m => m.EventType == type).Select(m => m.Payload).ToListAsync();
        return [.. payloads.Where(p => p.Contains(orderNumber, StringComparison.Ordinal))];
    }

    [Fact]
    [Trait("Spec", "BKG-1")]
    [Trait("Spec", "BKG-2")]
    public async Task APackedOrder_GetsOneShipment_BookedWithThePrimaryCourier_AndItsWaybill()
    {
        using var host = new ShippingHost(postgres);
        var (orderId, number) = host.PlaceOrder();

        var created = await host.SendAsync(new CreateShipmentCommand(orderId, number, 640));
        var outcome = await host.SendAsync(new BookShipmentCommand(created.ShipmentId!.Value));

        Assert.Equal(CreateShipmentOutcome.Created, created.Outcome);
        Assert.Equal(BookOutcome.Booked, outcome);
        var shipment = await ShippingHost.LoadAsync(postgres, created.ShipmentId.Value);
        Assert.Equal(ShipmentStatuses.Booked, shipment.StatusId);
        Assert.Equal(Couriers.Primary, shipment.CourierId);
        Assert.StartsWith("PRM-", shipment.Waybill);
        Assert.Equal((4700m, 640, BookingSources.Courier), (shipment.CashToCollect, shipment.WeightGrams, shipment.BookingSourceId));
        Assert.Single(await EventsAsync(postgres, "ShipmentBookedEvent", number));
    }

    [Fact]
    [Trait("Spec", "BKG-3")]
    public async Task WhenThePrimaryIsDown_TheSecondaryBooksAtOnce_AndTheShipmentRecordsWhich()
    {
        using var host = new ShippingHost(postgres);
        host.CourierState.Outage[Couriers.Primary] = true;
        var id = await host.NewShipmentAsync();

        var outcome = await host.SendAsync(new BookShipmentCommand(id));

        Assert.Equal(BookOutcome.Booked, outcome);
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal(Couriers.Secondary, shipment.CourierId);
        Assert.StartsWith("SEC-", shipment.Waybill);
    }

    [Fact]
    [Trait("Spec", "BKG-4")]
    public async Task WhenBothAreDown_TheShipmentIsPending_TriedAgainLater_AndOtherBookingsAreNotDelayed()
    {
        using var host = new ShippingHost(postgres);
        var stuck = await host.NewShipmentAsync();
        host.CourierState.Outage[Couriers.Primary] = true;
        host.CourierState.Outage[Couriers.Secondary] = true;

        var outcome = await host.SendAsync(new BookShipmentCommand(stuck));
        host.CourierState.Outage[Couriers.Primary] = false;
        var other = await host.NewShipmentAsync();
        var otherOutcome = await host.SendAsync(new BookShipmentCommand(other));

        Assert.Equal(BookOutcome.Unreachable, outcome);
        var pending = await ShippingHost.LoadAsync(postgres, stuck);
        Assert.Equal((ShipmentStatuses.PendingBooking, BookingErrors.Unreachable, 1), (pending.StatusId, pending.LastError, pending.Attempts));
        Assert.Equal(host.Options.RetryMinutes, (pending.NextAttemptAt!.Value - ShippingHost.Start).TotalMinutes);
        Assert.False(pending.NeedsStaff);
        Assert.Equal(BookOutcome.Booked, otherOutcome);
    }

    [Fact]
    [Trait("Spec", "BKG-5")]
    public async Task TheSameEventTwice_LeavesOneShipment_AndABookedShipmentIsNeverBookedAgain()
    {
        using var host = new ShippingHost(postgres);
        var (orderId, number) = host.PlaceOrder();
        var first = await host.SendAsync(new CreateShipmentCommand(orderId, number, 600));
        await host.SendAsync(new BookShipmentCommand(first.ShipmentId!.Value));
        var issuedBefore = host.CourierState.Issued.Count;

        var second = await host.SendAsync(new CreateShipmentCommand(orderId, number, 600));
        var again = await host.SendAsync(new BookShipmentCommand(first.ShipmentId.Value));

        Assert.Equal(CreateShipmentOutcome.AlreadyExists, second.Outcome);
        Assert.Equal(first.ShipmentId, second.ShipmentId);
        Assert.NotEqual(BookOutcome.Booked, again);
        Assert.Equal(issuedBefore, host.CourierState.Issued.Count);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Shipments.CountAsync(s => s.OrderId == orderId));
    }

    [Fact]
    [Trait("Spec", "BKG-6")]
    public async Task ARefusal_DoesNotTryTheSecondary_AndWaitsForStaffWithTheReason()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync(address: "REJECT lane");

        var outcome = await host.SendAsync(new BookShipmentCommand(id));

        Assert.Equal(BookOutcome.Rejected, outcome);
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal((BookingErrors.Rejected, true), (shipment.LastError, shipment.NeedsStaff));
        Assert.Contains("not serviceable", shipment.LastNote);
        Assert.DoesNotContain(host.CourierState.Issued, i => i.Value == shipment.OrderNumber);
    }

    [Fact]
    [Trait("Spec", "BKG-7")]
    public async Task TheAddressAndPhone_AreNeverStoredOrPublished_OnlyTheCashAndWeight()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync();
        await host.SendAsync(new BookShipmentCommand(id));

        await using var db = postgres.NewContext();
        var row = await db.Database.SqlQueryRaw<string>("SELECT row_to_json(s)::text AS \"Value\" FROM \"SHPG\".\"SHPG_MAIN\" s WHERE s.\"ID\" = {0}", id).SingleAsync();
        var events = (await EventsAsync(postgres, "ShipmentBookedEvent", (await ShippingHost.LoadAsync(postgres, id)).OrderNumber)).Single();

        foreach (var text in new[] { row, events })
        {
            Assert.DoesNotContain("Sara", text);
            Assert.DoesNotContain("0300", text);
            Assert.DoesNotContain("House 4", text);
        }
    }

    [Fact]
    [Trait("Spec", "BKG-8")]
    public async Task ACashOnDeliveryShipment_CarriesTheAmount_AndAPaidOneCarriesNone()
    {
        using var host = new ShippingHost(postgres);
        var cod = await host.NewShipmentAsync(cod: true);
        var paid = await host.NewShipmentAsync(cod: false);

        Assert.Equal(4700m, (await ShippingHost.LoadAsync(postgres, cod)).CashToCollect);
        Assert.Equal(0m, (await ShippingHost.LoadAsync(postgres, paid)).CashToCollect);
    }

    [Fact]
    [Trait("Spec", "BKG-9")]
    public async Task ABooking_PublishesTheBookedAndStatusEvents_WithIdsAndCodesOnly()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync();
        await host.SendAsync(new BookShipmentCommand(id));
        var shipment = await ShippingHost.LoadAsync(postgres, id);

        var booked = Assert.Single(await EventsAsync(postgres, "ShipmentBookedEvent", shipment.OrderNumber));
        var status = Assert.Single(await EventsAsync(postgres, "ShipmentStatusChangedEvent", shipment.OrderNumber));

        Assert.Contains(shipment.Waybill!, booked);
        Assert.Contains("PRIMARY", booked);
        Assert.Contains("BOOKED", status);
    }

    [Fact]
    [Trait("Spec", "BKG-5")]
    public async Task TwoWorkersBookingTheSameShipmentAtOnce_AskACourierOnce()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => host.SendAsync(new BookShipmentCommand(id))));

        Assert.Equal(1, outcomes.Count(o => o == BookOutcome.Booked));
        Assert.Single(host.CourierState.Issued);
    }

    [Fact]
    [Trait("Spec", "BKG-4")]
    public async Task TheWorkerPass_BooksEveryDueShipment_AndLeavesTheOnesNotDueOrWaitingForStaff()
    {
        using var host = new ShippingHost(postgres);
        var due = await host.NewShipmentAsync();
        var refused = await host.NewShipmentAsync(address: "REJECT street");
        await host.SendAsync(new BookShipmentCommand(refused));

        var booked = await host.SendAsync(new BookDueShipmentsCommand(100));

        Assert.True(booked >= 1);
        Assert.Equal(ShipmentStatuses.Booked, (await ShippingHost.LoadAsync(postgres, due)).StatusId);
        Assert.Equal(ShipmentStatuses.PendingBooking, (await ShippingHost.LoadAsync(postgres, refused)).StatusId);
    }

    [Fact]
    [Trait("Spec", "BKG-10")]
    public async Task ACancelledOrder_CancelsItsShipment_AndAskedTheCourierToDropTheBooking()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);

        var cancelled = await host.SendAsync(new CancelShipmentCommand(shipment.OrderId));

        Assert.True(cancelled);
        Assert.Equal(ShipmentStatuses.Cancelled, (await ShippingHost.LoadAsync(postgres, id)).StatusId);
        Assert.DoesNotContain(shipment.Waybill!, host.CourierState.Issued.Keys);
        Assert.False(await host.SendAsync(new CancelShipmentCommand(shipment.OrderId)));
        Assert.False(await host.SendAsync(new CancelShipmentCommand(987_654)));
    }

    [Fact]
    [Trait("Spec", "BKG-10")]
    public async Task AnOrderCancelledBeforeBooking_IsCancelledNotBooked()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        host.Orders.Views[shipment.OrderId] = host.Orders.Views[shipment.OrderId] with { StatusCode = "CNCL" };

        var outcome = await host.SendAsync(new BookShipmentCommand(id));

        Assert.Equal(BookOutcome.Cancelled, outcome);
        Assert.Equal(ShipmentStatuses.Cancelled, (await ShippingHost.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "BKG-1")]
    public async Task AnUnknownOrder_OrACancelledOne_HasNoShipment()
    {
        using var host = new ShippingHost(postgres);
        var (orderId, number) = host.PlaceOrder();
        host.Orders.Views[orderId] = host.Orders.Views[orderId] with { StatusCode = "CNCL" };

        Assert.Equal(CreateShipmentOutcome.NotEligible, (await host.SendAsync(new CreateShipmentCommand(orderId, number, 500))).Outcome);
        Assert.Equal(CreateShipmentOutcome.NotEligible, (await host.SendAsync(new CreateShipmentCommand(404_404, "RMP-X", 500))).Outcome);
        Assert.Equal(BookOutcome.NotFound, await host.SendAsync(new BookShipmentCommand(404_404_404)));
    }

    [Fact]
    [Trait("Spec", "BKG-11")]
    public async Task EveryShipmentRow_KeepsWhenItWasCreatedAndChanged()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync();
        await host.SendAsync(new BookShipmentCommand(id));

        var shipment = await ShippingHost.LoadAsync(postgres, id);

        Assert.NotEqual(default, shipment.InsrDte);
        Assert.NotNull(shipment.UpdtDte);
    }
}
