using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Shipping.Contracts;

/// <summary>
/// The shared envelope of every Shipping event (ADR 0004, ADR 0031). Payloads carry ids, the order number, codes and the waybill only: never a name,
/// address, phone or email (SEC-25). The order id is the aggregate id, so one order's events stay in order.
/// </summary>
public abstract record ShippingEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public string AggregateType => "Shipment";

    public abstract long AggregateId { get; }

    public short SchemaVersion => 1;
}

/// <summary>A courier booked the parcel and its waybill is attached to the order (BKG-9, FR-SC-13).</summary>
public sealed record ShipmentBookedEvent(long OrderId, string OrderNumber, string CourierCode, string Waybill) : ShippingEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>
/// The shipment moved to a new status (TRK-5). Orders moves its own status from it; Notifications tells the customer about a failed attempt.
/// <see cref="StatusCode"/> is one of <see cref="ShipmentStatusCodes"/>.
/// </summary>
public sealed record ShipmentStatusChangedEvent(long OrderId, string OrderNumber, string StatusCode, string Waybill) : ShippingEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>The stable codes of a shipment's status, used in events, the API and the tracking views.</summary>
public static class ShipmentStatusCodes
{
    public const string PendingBooking = "PENDING_BOOKING";
    public const string Booked = "BOOKED";
    public const string PickedUp = "PICKED_UP";
    public const string InTransit = "IN_TRANSIT";
    public const string OutForDelivery = "OUT_FOR_DELIVERY";
    public const string FailedAttempt = "FAILED_ATTEMPT";
    public const string Delivered = "DELIVERED";
    public const string ReturnedToOrigin = "RETURNED_TO_ORIGIN";
    public const string Cancelled = "CANCELLED";
}

/// <summary>One step of the timeline the customer sees: a status and when it happened.</summary>
public sealed record TrackingStep(string StatusCode, string StatusName, DateTimeOffset At);

/// <summary>
/// What a customer, or a stranger holding a tracking number, may see of a shipment: the courier, the waybill, the current status and the timeline.
/// Nothing else: no order data, no staff names, no address (OHS-5, TRK-9).
/// </summary>
public sealed record ShipmentTrackingView(string CourierName, string Waybill, string StatusCode, string StatusName, IReadOnlyList<TrackingStep> Steps);

/// <summary>How Orders (and the public look-up) read tracking without touching Shipping's tables (ADR 0002).</summary>
public interface IShipmentTrackingQueries
{
    /// <summary>The tracking of an order's shipment once it has a waybill, or null while the parcel is still being prepared (OHS-3).</summary>
    Task<ShipmentTrackingView?> FindByOrderAsync(long orderId, CancellationToken cancellationToken);

    /// <summary>The tracking of the shipment with this waybill, or null (TRK-9).</summary>
    Task<ShipmentTrackingView?> FindByWaybillAsync(string waybill, CancellationToken cancellationToken);
}
