namespace Romp.Modules.Shipping.Application.CourierPort;

/// <summary>
/// Everything a courier needs to take a parcel (CPT-1, CPT-8): where it goes, who to call, the cash to collect and the weight. It is built from Orders at
/// the moment of booking and is never stored or logged by Shipping.
/// </summary>
public sealed record BookingRequest(
    string OrderNumber, string RecipientName, string Phone, string AddressLine1, string? AddressLine2, string City, string Province, string? PostalCode,
    string? DeliveryNote, decimal CashToCollect, int WeightGrams);

public enum BookingOutcome
{
    /// <summary>The courier took the parcel and gave a waybill.</summary>
    Booked,

    /// <summary>The courier could not be reached or timed out: try another, or try later (CPT-2).</summary>
    Unreachable,

    /// <summary>The courier refused this parcel (bad address): another try would not help (CPT-3).</summary>
    Rejected,
}

public sealed record BookingResult(BookingOutcome Outcome, string? Waybill = null, string? Reason = null)
{
    public static BookingResult Booked(string waybill) => new(BookingOutcome.Booked, waybill);

    public static BookingResult Unreachable() => new(BookingOutcome.Unreachable);

    public static BookingResult Rejected(string reason) => new(BookingOutcome.Rejected, null, reason);
}

/// <summary>One status of a shipment as the courier reports it (by push or by polling): a status code, the courier's event id and when it happened (CPT-4, CPT-5).</summary>
public sealed record CourierStatus(string StatusCode, string CourierEventId, DateTimeOffset At);

/// <summary>A verified push: the waybill it is about and the status it reports.</summary>
public sealed record CourierPush(string Waybill, CourierStatus Status);

/// <summary>
/// One interface to a courier (ADR 0031). It never throws for an ordinary courier problem: an outage is <see cref="BookingOutcome.Unreachable"/>, a
/// refusal is <see cref="BookingOutcome.Rejected"/> (CPT-2, CPT-3). A real courier is a new class behind this port; the fake is the only one built.
/// </summary>
public interface ICourierGateway
{
    /// <summary>The courier's id in <c>Couriers</c>.</summary>
    short CourierId { get; }

    /// <summary>True when the courier pushes status; false means its shipments are polled (TRK-6).</summary>
    bool PushesStatus { get; }

    Task<BookingResult> BookAsync(BookingRequest request, CancellationToken cancellationToken);

    /// <summary>The courier's current status of a waybill, or null when it does not know it (polling fallback, CPT-4).</summary>
    Task<CourierStatus?> StatusAsync(string waybill, CancellationToken cancellationToken);

    /// <summary>Asks the courier to drop a booking (BKG-10). False when it cannot be done by the port.</summary>
    Task<bool> CancelAsync(string waybill, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the signature over the raw body and reads the push; null when the signature does not verify or the body is not a status (CPT-5, TRK-2).
    /// The comparison is constant time.
    /// </summary>
    CourierPush? ParsePush(string body, string? signature);
}

/// <summary>Finds the couriers by id or code, and the order they are tried in at booking: primary first (CPT-7, BKG-3).</summary>
public interface ICourierRegistry
{
    /// <summary>The enabled couriers in the order a booking tries them.</summary>
    IReadOnlyList<ICourierGateway> InOrder { get; }

    ICourierGateway? Find(short courierId);

    ICourierGateway? FindByCode(string? code);
}
