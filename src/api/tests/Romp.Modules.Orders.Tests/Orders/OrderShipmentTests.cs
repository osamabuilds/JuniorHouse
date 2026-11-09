using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.OrderLookup;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Tests.Support;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Orders.Tests.Orders;

/// <summary>S15 O1 to O3: Shipping reads where a parcel goes, the order follows the courier, and the customer sees the tracking (OHS-1 to OHS-5, TRK-5).</summary>
public sealed class OrderShipmentTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    private static string Ip() => $"ip-{Interlocked.Increment(ref _seq)}-{Guid.NewGuid():N}";

    private static async Task<(long Id, string Number, string Mobile)> ConfirmedAsync(OrdersHost host, bool confirm = true)
    {
        var mobile = $"03{Interlocked.Increment(ref _seq) + 700_000_000:000000000}";
        var placed = await host.SendAsync(new PlaceOrderCommand(
            Guid.NewGuid().ToString("N"),
            new CheckoutContact("Ayesha Khan", "a@example.com", mobile, false),
            new CheckoutAddress("Ayesha Khan", mobile, "House 4, Street 2", "Near the park", 2, 7, "54000", "Call first"), null, PaymentMethods.CashOnDelivery));
        if (confirm)
        {
            await host.SendAsync(new RecordCodOutcomeCommand(placed.OrderNumber!, CodOutcomes.Confirmed, "1", null));
        }

        return (placed.OrderId!.Value, placed.OrderNumber!, mobile);
    }

    private async Task<(short Status, List<short> History)> StateAsync(long id)
    {
        await using var db = postgres.NewContext();
        var order = await db.Orders.AsNoTracking().Include(o => o.History).SingleAsync(o => o.Id == id);
        return (order.StatusId, [.. order.History.OrderBy(h => h.Id).Select(h => h.ToStatusId)]);
    }

    [Fact]
    [Trait("Spec", "BKG-7")]
    public async Task ShippingMayReadTheRecipientAddressPhoneAndCashToCollect_OfARealOrder()
    {
        using var host = new OrdersHost(postgres);
        var (id, number, _) = await ConfirmedAsync(host);
        await using var db = postgres.NewContext();

        var view = await new OrderShippingQueries(db).FindAsync(id, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(number, view.OrderNumber);
        Assert.Equal(("Ayesha Khan", "House 4, Street 2", "Near the park"), (view.RecipientName, view.AddressLine1, view.AddressLine2));
        Assert.True(view.IsCashOnDelivery);
        Assert.True(view.CashToCollect > 0);
        Assert.Null(await new OrderShippingQueries(db).FindAsync(987_654_321, CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "OHS-4")]
    public async Task APickUp_MakesTheOrderDispatched_AndADelivery_Delivered_RecordedAsTheCourier()
    {
        using var host = new OrdersHost(postgres);
        var (id, _, _) = await ConfirmedAsync(host);

        await host.SendAsync(new ApplyShipmentStatusCommand(id, ShipmentStatusCodes.PickedUp));
        var dispatched = await StateAsync(id);
        await host.SendAsync(new ApplyShipmentStatusCommand(id, ShipmentStatusCodes.Delivered));
        var delivered = await StateAsync(id);

        Assert.Equal(OrderStatuses.Dispatched, dispatched.Status);
        Assert.Equal(OrderStatuses.Delivered, delivered.Status);
        await using var db = postgres.NewContext();
        var last = await db.OrderHistory.AsNoTracking().Where(h => h.OrderId == id).OrderByDescending(h => h.Id).FirstAsync();
        Assert.Equal((ActorKinds.System, "courier"), (last.ActorKindId, last.ActorRef));
    }

    [Fact]
    [Trait("Spec", "OHS-4")]
    public async Task ARepeatedStatus_ChangesNothing_AndADeliveryWithoutAPickUpStillPassesThroughDispatched()
    {
        using var host = new OrdersHost(postgres);
        var (id, _, _) = await ConfirmedAsync(host);

        Assert.True(await host.SendAsync(new ApplyShipmentStatusCommand(id, ShipmentStatusCodes.Delivered)));
        var historyOnce = (await StateAsync(id)).History;
        Assert.False(await host.SendAsync(new ApplyShipmentStatusCommand(id, ShipmentStatusCodes.Delivered)));

        Assert.Equal([OrderStatuses.PendingConfirmation, OrderStatuses.Confirmed, OrderStatuses.Dispatched, OrderStatuses.Delivered], historyOnce);
        Assert.Equal(historyOnce, (await StateAsync(id)).History);
    }

    [Theory]
    [Trait("Spec", "OHS-4")]
    [InlineData(ShipmentStatusCodes.Booked)]
    [InlineData(ShipmentStatusCodes.InTransit)]
    [InlineData(ShipmentStatusCodes.FailedAttempt)]
    [InlineData("SOMETHING_ELSE")]
    public async Task OtherCourierStatuses_AreForTheTimeline_NotForTheOrderStatus(string code)
    {
        using var host = new OrdersHost(postgres);
        var (id, _, _) = await ConfirmedAsync(host);

        Assert.False(await host.SendAsync(new ApplyShipmentStatusCommand(id, code)));
        Assert.Equal(OrderStatuses.Confirmed, (await StateAsync(id)).Status);
    }

    [Fact]
    [Trait("Spec", "OHS-4")]
    public async Task AnOrderThatIsNotConfirmedOrIsCancelled_IsNotMovedByTheCourier()
    {
        using var host = new OrdersHost(postgres);
        var (waiting, _, _) = await ConfirmedAsync(host, confirm: false);
        var (cancelled, number, _) = await ConfirmedAsync(host);
        await host.SendAsync(new CancelOrderCommand(number, CancelReasons.Staff, ActorKinds.Staff, "1", null));

        Assert.False(await host.SendAsync(new ApplyShipmentStatusCommand(waiting, ShipmentStatusCodes.PickedUp)));
        Assert.False(await host.SendAsync(new ApplyShipmentStatusCommand(cancelled, ShipmentStatusCodes.PickedUp)));
        Assert.False(await host.SendAsync(new ApplyShipmentStatusCommand(404_404_404, ShipmentStatusCodes.PickedUp)));
        Assert.Equal(OrderStatuses.PendingConfirmation, (await StateAsync(waiting)).Status);
        Assert.Equal(OrderStatuses.Cancelled, (await StateAsync(cancelled)).Status);
    }

    private static readonly ShipmentTrackingView Tracking = new(
        "Primary courier", "PRM-000007-1234", "IN_TRANSIT", "On its way",
        [new TrackingStep("BOOKED", "Booked with the courier", new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero)), new TrackingStep("IN_TRANSIT", "On its way", new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero))]);

    [Fact]
    [Trait("Spec", "OHS-2")]
    public async Task TheGuestLookup_ShowsTheCourierWaybillAndTimeline_WhenThereIsAShipment()
    {
        using var host = new OrdersHost(postgres);
        var (_, number, mobile) = await ConfirmedAsync(host);
        host.Tracking.View = Tracking;

        var result = await host.SendAsync(new LookupOrderQuery(number, mobile, null, Ip()));

        Assert.Equal(Tracking, result.Order!.Tracking);
    }

    [Fact]
    [Trait("Spec", "OHS-3")]
    public async Task WithoutAShipment_TheTrackingBlockIsEmpty_SoTheScreenSaysTheParcelIsBeingPrepared()
    {
        using var host = new OrdersHost(postgres);
        var (_, number, mobile) = await ConfirmedAsync(host);
        host.Tracking.View = null;

        var result = await host.SendAsync(new LookupOrderQuery(number, mobile, null, Ip()));

        Assert.Equal(LookupStatus.Found, result.Status);
        Assert.Null(result.Order!.Tracking);
    }

    [Fact]
    [Trait("Spec", "OHS-5")]
    public async Task TheTrackingBlock_CarriesOnlyCourierWaybillStatusesAndTimes()
    {
        using var host = new OrdersHost(postgres);
        var (_, number, mobile) = await ConfirmedAsync(host);
        host.Tracking.View = Tracking;
        var result = await host.SendAsync(new LookupOrderQuery(number, mobile, null, Ip()));

        var json = JsonSerializer.Serialize(result.Order!.Tracking);

        Assert.DoesNotContain("Ayesha", json);
        Assert.DoesNotContain("0300", json);
        Assert.DoesNotContain(number, json);
    }
}
