using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;

namespace Romp.Modules.Shipping.Tests.Bookings;

/// <summary>S14 S2: the Shipment aggregate's own rules, with no database (BKG-2, BKG-5, TRK-3, TRK-4, MBQ-6, MBQ-8).</summary>
public sealed class ShipmentDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private static Shipment Pending() => Shipment.Create(5, "RMP-5", 4700m, 600, T0);

    private static Shipment Booked()
    {
        var s = Pending();
        s.MarkBooked(Couriers.Primary, "PRM-000001", BookingSources.Courier, T0);
        return s;
    }

    [Fact]
    [Trait("Spec", "BKG-2")]
    public void Booking_StoresTheWaybillAndCourier_AndRaisesTheBookedAndStatusEvents()
    {
        var s = Pending();

        var booked = s.MarkBooked(Couriers.Primary, "prm-000001", BookingSources.Courier, T0);

        Assert.True(booked);
        Assert.Equal(("PRM-000001", Couriers.Primary, ShipmentStatuses.Booked), (s.Waybill, s.CourierId, s.StatusId));
        Assert.Contains(s.DomainEvents, e => e is ShipmentBookedEvent b && b.Waybill == "PRM-000001" && b.CourierCode == "PRIMARY");
        Assert.Contains(s.DomainEvents, e => e is ShipmentStatusChangedEvent c && c.StatusCode == ShipmentStatusCodes.Booked);
    }

    [Fact]
    [Trait("Spec", "BKG-5")]
    public void ABookedShipment_KeepsItsFirstWaybill()
    {
        var s = Booked();

        var again = s.MarkBooked(Couriers.Secondary, "SEC-000002", BookingSources.Courier, T0.AddMinutes(1));

        Assert.False(again);
        Assert.Equal("PRM-000001", s.Waybill);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A B")]
    [InlineData("A;DROP")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void AMalformedWaybill_IsRefused(string waybill) =>
        Assert.Throws<DomainException>(() => Pending().MarkBooked(Couriers.Primary, waybill, BookingSources.Manual, T0));

    [Fact]
    [Trait("Spec", "MBQ-6")]
    public void AnOutage_IsTriedAgainLater_UntilTheAttemptsAreUsedUp_ThenWaitsForStaff()
    {
        var s = Pending();

        s.RecordBookingFailure(BookingErrors.Unreachable, null, T0, retryMinutes: 5, maxAttempts: 3);
        Assert.False(s.NeedsStaff);
        Assert.Equal(T0.AddMinutes(5), s.NextAttemptAt);
        s.RecordBookingFailure(BookingErrors.Unreachable, null, T0, 5, 3);
        s.RecordBookingFailure(BookingErrors.Unreachable, null, T0, 5, 3);

        Assert.True(s.NeedsStaff);
        Assert.Null(s.NextAttemptAt);
        Assert.Equal(3, s.Attempts);
    }

    [Fact]
    [Trait("Spec", "MBQ-8")]
    public void ARefusal_WaitsForStaffAtOnce_WithACleanedShortReason()
    {
        var s = Pending();

        s.RecordBookingFailure(BookingErrors.Rejected, "Bad <address> 03001234567!!", T0, 5, 12);

        Assert.True(s.NeedsStaff);
        Assert.Null(s.NextAttemptAt);
        Assert.Equal("Bad address 03001234567", s.LastNote);
    }

    [Fact]
    [Trait("Spec", "MBQ-3")]
    public void TryingAgainNow_MakesItDueAtOnce_AndStartsTheCountAgain()
    {
        var s = Pending();
        s.RecordBookingFailure(BookingErrors.Rejected, "x", T0, 5, 12);

        var asked = s.RequestRetry(T0.AddHours(1));

        Assert.True(asked);
        Assert.False(s.NeedsStaff);
        Assert.Equal((0, T0.AddHours(1)), (s.Attempts, s.NextAttemptAt));
        Assert.False(Booked().RequestRetry(T0));
    }

    [Theory]
    [Trait("Spec", "TRK-4")]
    [InlineData(ShipmentStatuses.PickedUp, ShipmentStatuses.InTransit)]
    [InlineData(ShipmentStatuses.InTransit, ShipmentStatuses.OutForDelivery)]
    [InlineData(ShipmentStatuses.OutForDelivery, ShipmentStatuses.Delivered)]
    [InlineData(ShipmentStatuses.OutForDelivery, ShipmentStatuses.FailedAttempt)]
    [InlineData(ShipmentStatuses.FailedAttempt, ShipmentStatuses.OutForDelivery)]
    public void AStatusMovesForward_AndAReattemptFollowsAFailure(short from, short to)
    {
        var s = Booked();
        s.Apply(from, "e1", T0.AddMinutes(1), EventSources.Push);

        var outcome = s.Apply(to, "e2", T0.AddMinutes(2), EventSources.Push);

        Assert.Equal(ApplyOutcome.Moved, outcome);
        Assert.Equal(to, s.StatusId);
    }

    [Fact]
    [Trait("Spec", "TRK-4")]
    public void AnOlderStatusArrivingLate_IsStored_ButNeverMovesTheShipmentBack()
    {
        var s = Booked();
        s.Apply(ShipmentStatuses.OutForDelivery, "e1", T0.AddMinutes(10), EventSources.Push);

        var outcome = s.Apply(ShipmentStatuses.InTransit, "e0", T0.AddMinutes(5), EventSources.Push);

        Assert.Equal(ApplyOutcome.Stale, outcome);
        Assert.Equal(ShipmentStatuses.OutForDelivery, s.StatusId);
        Assert.Equal(2, s.Events.Count);
    }

    [Fact]
    [Trait("Spec", "TRK-3")]
    public void TheSameCourierEventTwice_ChangesNothing()
    {
        var s = Booked();
        s.Apply(ShipmentStatuses.PickedUp, "same", T0.AddMinutes(1), EventSources.Push);

        var again = s.Apply(ShipmentStatuses.PickedUp, "same", T0.AddMinutes(1), EventSources.Push);

        Assert.Equal(ApplyOutcome.Duplicate, again);
        Assert.Single(s.Events);
        Assert.Equal(2, s.DomainEvents.OfType<ShipmentStatusChangedEvent>().Count());
    }

    [Fact]
    [Trait("Spec", "TRK-1")]
    public void AFinishedShipment_NeverChanges_AndANotYetBookedOneIgnoresStatuses()
    {
        var done = Booked();
        done.Apply(ShipmentStatuses.Delivered, "e1", T0.AddHours(5), EventSources.Push);
        var pending = Pending();

        Assert.Equal(ApplyOutcome.Ignored, done.Apply(ShipmentStatuses.InTransit, "e2", T0.AddHours(6), EventSources.Push));
        Assert.Equal(ApplyOutcome.Ignored, pending.Apply(ShipmentStatuses.PickedUp, "e3", T0, EventSources.Push));
        Assert.Equal(T0.AddHours(5), done.DeliveredAt);
    }

    [Fact]
    [Trait("Spec", "TRK-7")]
    public void AFailedAttempt_IsCounted_AndRaisesTheStatusEventForTheCustomerNotice()
    {
        var s = Booked();
        s.Apply(ShipmentStatuses.OutForDelivery, "e1", T0.AddHours(1), EventSources.Push);

        s.Apply(ShipmentStatuses.FailedAttempt, "e2", T0.AddHours(2), EventSources.Push);
        s.Apply(ShipmentStatuses.OutForDelivery, "e3", T0.AddHours(26), EventSources.Push);
        s.Apply(ShipmentStatuses.FailedAttempt, "e4", T0.AddHours(27), EventSources.Push);

        Assert.Equal(2, s.FailedAttempts);
        Assert.Equal(2, s.DomainEvents.OfType<ShipmentStatusChangedEvent>().Count(e => e.StatusCode == ShipmentStatusCodes.FailedAttempt));
    }

    [Theory]
    [Trait("Spec", "TRK-8")]
    [InlineData(ShipmentStatuses.PendingBooking)]
    [InlineData(ShipmentStatuses.Booked)]
    [InlineData(ShipmentStatuses.Cancelled)]
    [InlineData(99)]
    public void ACourierCannotReportABookingOrCancellation(short status) =>
        Assert.Throws<DomainException>(() => Booked().Apply(status, "e1", T0, EventSources.Push));

    [Fact]
    [Trait("Spec", "BKG-10")]
    public void CancellingBeforeCollection_Works_AndTellsWhetherTheCourierMustBeTold()
    {
        var unbooked = Pending();
        var booked = Booked();
        var collected = Booked();
        collected.Apply(ShipmentStatuses.PickedUp, "e1", T0.AddMinutes(1), EventSources.Push);

        Assert.Equal((true, false), unbooked.Cancel(T0));
        Assert.Equal((true, true), booked.Cancel(T0));
        Assert.Equal((false, false), collected.Cancel(T0));
        Assert.Equal(ShipmentStatuses.Cancelled, booked.StatusId);
    }

    [Fact]
    [Trait("Spec", "MNF-3")]
    public void OnlyABookedUncollectedParcel_GoesOnOneManifest()
    {
        var s = Booked();
        s.PutOnManifest(7);

        Assert.Throws<DomainException>(() => s.PutOnManifest(8));
        Assert.Throws<DomainException>(() => Pending().PutOnManifest(7));
        s.TakeOffManifest();
        s.PutOnManifest(8);
        Assert.Equal(8, s.ManifestId);
    }

    [Fact]
    [Trait("Spec", "LBL-5")]
    public void ALabelCannotBePrintedBeforeABooking_AndEachPrintIsCounted()
    {
        Assert.Throws<DomainException>(() => Pending().RecordLabelPrinted());
        var s = Booked();

        s.RecordLabelPrinted();
        s.RecordLabelPrinted();

        Assert.Equal(2, s.LabelPrintCount);
    }

    [Fact]
    [Trait("Spec", "BKG-1")]
    public void AShipmentNeedsAnOrderNumberAWeightAndAnAmount()
    {
        Assert.Throws<DomainException>(() => Shipment.Create(1, "", 0m, 100, T0));
        Assert.Throws<DomainException>(() => Shipment.Create(1, "RMP-1", -1m, 100, T0));
        Assert.Throws<DomainException>(() => Shipment.Create(1, "RMP-1", 0m, 0, T0));
    }
}
