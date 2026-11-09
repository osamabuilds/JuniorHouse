using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.Tracking;

/// <summary>S14 S8 and S9: status pushes and polling, the failed-attempt flag, the timeline, and the public look-up by tracking number (TRK-1 to TRK-13).</summary>
[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class TrackingTests(ShippingPostgres postgres)
{
    private static async Task<List<string>> StatusEventsAsync(ShippingPostgres postgres, string orderNumber)
    {
        await using var db = postgres.NewContext();
        var payloads = await db.Set<OutboxMessage>().Where(m => m.EventType == "ShipmentStatusChangedEvent").Select(m => m.Payload).ToListAsync();
        return [.. payloads.Where(p => p.Contains(orderNumber, StringComparison.Ordinal))];
    }

    private static async Task<PushOutcome> SendPushAsync(ShippingHost host, string waybill, string status, string? eventId = null, DateTimeOffset? at = null, string courier = "PRIMARY")
    {
        var (body, signature) = host.Push(waybill, status, eventId, at);
        return await host.SendAsync(new ApplyCourierPushCommand(courier, body, signature));
    }

    [Fact]
    [Trait("Spec", "TRK-1")]
    public async Task AValidPush_MovesTheShipment_StoresTheEventOnce_AndPublishesTheStatus()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);

        var outcome = await SendPushAsync(host, shipment.Waybill!, "PICKED_UP");

        Assert.Equal(PushOutcome.Accepted, outcome);
        var after = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal(ShipmentStatuses.PickedUp, after.StatusId);
        Assert.Single(after.Events);
        Assert.Contains(await StatusEventsAsync(postgres, shipment.OrderNumber), p => p.Contains("PICKED_UP", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Spec", "TRK-2")]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("other-secret")]
    public async Task ABadSignature_ChangesNothing_AndIsTheSameRefusalAsAnUnknownWaybill(string kind)
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        var (body, good) = host.Push(shipment.Waybill!, "DELIVERED");
        var signature = kind switch
        {
            "missing" => null,
            "wrong" => good[..^2] + "00",
            _ => host.Push(shipment.Waybill!, "DELIVERED", secret: "another-secret-0123456789abcdef").Signature,
        };

        var bad = await host.SendAsync(new ApplyCourierPushCommand("PRIMARY", body, signature));
        var (unknownBody, unknownSig) = host.Push("PRM-NOSUCH", "DELIVERED");
        var unknown = await host.SendAsync(new ApplyCourierPushCommand("PRIMARY", unknownBody, unknownSig));
        var nobody = await host.SendAsync(new ApplyCourierPushCommand("NOBODY", body, good));

        Assert.Equal(PushOutcome.Rejected, bad);
        Assert.Equal(bad, unknown);
        Assert.Equal(bad, nobody);
        Assert.Equal(ShipmentStatuses.Booked, (await ShippingHost.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "TRK-3")]
    public async Task TheSameCourierEventTwice_ChangesNothing_AndStillAnswersSuccess()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;

        var first = await SendPushAsync(host, waybill, "PICKED_UP", "evt-dup", host.Time.Now);
        var second = await SendPushAsync(host, waybill, "PICKED_UP", "evt-dup", host.Time.Now);

        Assert.Equal((PushOutcome.Accepted, PushOutcome.Accepted), (first, second));
        Assert.Single((await ShippingHost.LoadAsync(postgres, id)).Events);
    }

    [Fact]
    [Trait("Spec", "TRK-4")]
    public async Task AnOlderStatusArrivingLate_IsKept_ButDoesNotMoveTheShipmentBack()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        await SendPushAsync(host, waybill, "OUT_FOR_DELIVERY", at: host.Time.Now.AddHours(5));

        await SendPushAsync(host, waybill, "IN_TRANSIT", at: host.Time.Now.AddHours(2));

        var after = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal(ShipmentStatuses.OutForDelivery, after.StatusId);
        Assert.Equal(2, after.Events.Count);
    }

    [Fact]
    [Trait("Spec", "TRK-4")]
    public async Task APushThatIsNotAStatusACourierCanReport_IsRefused()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;

        Assert.Equal(PushOutcome.Rejected, await SendPushAsync(host, waybill, "BOOKED"));
        Assert.Equal(PushOutcome.Rejected, await SendPushAsync(host, waybill, "CANCELLED"));
        Assert.Equal(PushOutcome.Rejected, await SendPushAsync(host, waybill, "NONSENSE"));
    }

    [Fact]
    [Trait("Spec", "TRK-6")]
    public async Task ACourierThatDoesNotPush_IsPolled_AndTheAnswerIsAppliedLikeAPush()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync("SECONDARY");
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        host.CourierState.Script[waybill] = new CourierStatus("IN_TRANSIT", "poll-1", host.Time.Now.AddHours(1));

        var moved = await host.SendAsync(new PollShipmentsCommand(100));
        var again = await host.SendAsync(new PollShipmentsCommand(100));

        Assert.True(moved >= 1);
        Assert.Equal(0, again);
        var after = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal(ShipmentStatuses.InTransit, after.StatusId);
        Assert.Equal(EventSources.Poll, after.Events.Single().SourceId);
    }

    [Fact]
    [Trait("Spec", "TRK-6")]
    public async Task APushingCourier_IsNotPolled_AndAPolledShipmentIsAskedAgainAfterThePollInterval()
    {
        using var host = new ShippingHost(postgres);
        var pushed = await host.NewBookedShipmentAsync("PRIMARY");
        var polled = await host.NewBookedShipmentAsync("SECONDARY");
        var waybill = (await ShippingHost.LoadAsync(postgres, polled)).Waybill!;
        host.CourierState.Script[waybill] = new CourierStatus("PICKED_UP", "poll-a", host.Time.Now.AddHours(1));
        await host.SendAsync(new PollShipmentsCommand(100));
        host.CourierState.Script[waybill] = new CourierStatus("OUT_FOR_DELIVERY", "poll-b", host.Time.Now.AddHours(3));

        var tooSoon = await host.SendAsync(new PollShipmentsCommand(100));
        host.Time.Now = ShippingHost.Start.AddMinutes(host.Options.PollMinutes + 1);
        var later = await host.SendAsync(new PollShipmentsCommand(100));

        Assert.Equal(0, tooSoon);
        Assert.True(later >= 1);
        Assert.Equal(ShipmentStatuses.OutForDelivery, (await ShippingHost.LoadAsync(postgres, polled)).StatusId);
        Assert.Empty((await ShippingHost.LoadAsync(postgres, pushed)).Events);
    }

    [Fact]
    [Trait("Spec", "TRK-6")]
    public async Task ACourierThatIsDownWhilePolled_IsSkipped_NotAnError()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync("SECONDARY");
        host.CourierState.Outage[Couriers.Secondary] = true;

        var moved = await host.SendAsync(new PollShipmentsCommand(100));

        Assert.Equal(0, moved);
        Assert.Equal(ShipmentStatuses.Booked, (await ShippingHost.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "TRK-7")]
    public async Task AFailedAttempt_IsFlaggedForStaff_AndPublishedForTheCustomerNotice()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        await SendPushAsync(host, shipment.Waybill!, "OUT_FOR_DELIVERY", at: host.Time.Now.AddHours(1));

        await SendPushAsync(host, shipment.Waybill!, "FAILED_ATTEMPT", at: host.Time.Now.AddHours(2));

        var row = (await host.SendAsync(new ListShipmentsQuery("FAILED_ATTEMPT", 1, 100))).Items.Single(r => r.Id == id);
        Assert.Equal(1, row.FailedAttempts);
        Assert.Contains(await StatusEventsAsync(postgres, shipment.OrderNumber), p => p.Contains("FAILED_ATTEMPT", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "TRK-8")]
    public async Task AReturnToOrigin_IsTheFinalStatus_AndNothingIsRestockedHere()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        await SendPushAsync(host, waybill, "RETURNED_TO_ORIGIN", at: host.Time.Now.AddDays(5));

        var outcome = await SendPushAsync(host, waybill, "IN_TRANSIT", at: host.Time.Now.AddDays(6));

        Assert.Equal(PushOutcome.Accepted, outcome);
        Assert.Equal(ShipmentStatuses.ReturnedToOrigin, (await ShippingHost.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "TRK-12")]
    public async Task ADeliveredCashOnDeliveryShipment_KeepsTheDeliveredTimeAndTheAmount()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        var when = host.Time.Now.AddDays(2);

        await SendPushAsync(host, waybill, "DELIVERED", at: when);

        var shipment = await ShippingHost.LoadAsync(postgres, id);
        Assert.Equal((when, 4700m), (shipment.DeliveredAt, shipment.CashToCollect));
    }

    [Fact]
    [Trait("Spec", "TRK-13")]
    public async Task StaffSeeTheFullTimeline_AndTheBookingHistory()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync("SECONDARY");
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        host.CourierState.Script[waybill] = new CourierStatus("PICKED_UP", "p1", host.Time.Now.AddHours(1));
        await host.SendAsync(new PollShipmentsCommand(100));

        var detail = (await host.SendAsync(new GetShipmentQuery(id)))!;

        Assert.Equal(("COURIER", "Secondary courier", waybill), (detail.BookingSource, detail.Row.CourierName, detail.Row.Waybill));
        var step = Assert.Single(detail.Events);
        Assert.Equal(("PICKED_UP", "POLL"), (step.Status, step.Source));
        Assert.Null(await host.SendAsync(new GetShipmentQuery(404_404)));
    }

    // ---------------------------------------------------------------- the customer's view and the public look-up

    [Fact]
    [Trait("Spec", "OHS-1")]
    public async Task TheCustomersTimeline_ShowsTheBookingAndEachStatusThatMovedTheParcelForward()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        await SendPushAsync(host, shipment.Waybill!, "PICKED_UP", at: host.Time.Now.AddHours(1));
        await SendPushAsync(host, shipment.Waybill!, "OUT_FOR_DELIVERY", at: host.Time.Now.AddHours(5));
        await SendPushAsync(host, shipment.Waybill!, "IN_TRANSIT", at: host.Time.Now.AddHours(3));

        var view = await host.InScopeAsync(sp => sp.GetRequiredService<IShipmentTrackingQueries>().FindByOrderAsync(shipment.OrderId, CancellationToken.None));

        Assert.NotNull(view);
        Assert.Equal(("Primary courier", shipment.Waybill, "OUT_FOR_DELIVERY"), (view.CourierName, view.Waybill, view.StatusCode));
        Assert.Equal(["BOOKED", "PICKED_UP", "IN_TRANSIT", "OUT_FOR_DELIVERY"], view.Steps.Select(s => s.StatusCode));
    }

    [Fact]
    [Trait("Spec", "OHS-3")]
    public async Task BeforeABooking_OrAfterACancel_ThereIsNoTrackingToShow()
    {
        using var host = new ShippingHost(postgres);
        var pending = await host.NewShipmentAsync();
        var booked = await host.NewBookedShipmentAsync();
        var bookedShipment = await ShippingHost.LoadAsync(postgres, booked);
        await host.SendAsync(new Romp.Modules.Shipping.Application.Bookings.CancelShipmentCommand(bookedShipment.OrderId));

        var pendingOrder = (await ShippingHost.LoadAsync(postgres, pending)).OrderId;

        Task<ShipmentTrackingView?> Find(long orderId) =>
            host.InScopeAsync(sp => sp.GetRequiredService<IShipmentTrackingQueries>().FindByOrderAsync(orderId, CancellationToken.None));

        Assert.Null(await Find(pendingOrder));
        Assert.Null(await Find(bookedShipment.OrderId));
        Assert.Null(await Find(404_404));
    }

    [Fact]
    [Trait("Spec", "OHS-5")]
    [Trait("Spec", "TRK-9")]
    public async Task ATrackingNumberAlone_ShowsOnlyCourierStatusAndTimes_NoOrderData()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);

        var result = await host.SendAsync(new TrackByWaybillQuery(shipment.Waybill!.ToLowerInvariant(), null, "10.0.0.1"));

        Assert.Equal(TrackLookupStatus.Found, result.Status);
        var json = JsonSerializer.Serialize(result.Tracking);
        Assert.Contains(shipment.Waybill, json);
        foreach (var leak in new[] { shipment.OrderNumber, "Sara", "0300", "Lahore", "House", "4700" })
        {
            Assert.DoesNotContain(leak, json);
        }
    }

    [Theory]
    [Trait("Spec", "TRK-10")]
    [InlineData("PRM-NOSUCH")]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("'; DROP TABLE \"SHPG\".\"SHPG_MAIN\"; --")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AnyMiss_IsTheSameAnswer(string waybill)
    {
        using var host = new ShippingHost(postgres);

        var result = await host.SendAsync(new TrackByWaybillQuery(waybill, null, "10.0.0.2"));

        Assert.Equal(new TrackLookupResult(TrackLookupStatus.NotFound), result);
    }

    [Fact]
    [Trait("Spec", "TRK-10")]
    public async Task AfterAFewFailures_ACaptchaIsRequired_AndAPassedOneLetsTheLookupThrough()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        for (var i = 0; i < host.Options.TrackingFailuresBeforeCaptcha; i++)
        {
            await host.SendAsync(new TrackByWaybillQuery("PRM-NOPE" + i, null, "10.0.0.3"));
        }

        var blocked = await host.SendAsync(new TrackByWaybillQuery(waybill, null, "10.0.0.3"));
        var elsewhere = await host.SendAsync(new TrackByWaybillQuery(waybill, null, "10.0.0.4"));
        host.Captcha.Passes = true;
        var passed = await host.SendAsync(new TrackByWaybillQuery(waybill, "ok", "10.0.0.3"));

        Assert.Equal(TrackLookupStatus.CaptchaRequired, blocked.Status);
        Assert.Equal(TrackLookupStatus.Found, elsewhere.Status);
        Assert.Equal(TrackLookupStatus.Found, passed.Status);
    }
}
