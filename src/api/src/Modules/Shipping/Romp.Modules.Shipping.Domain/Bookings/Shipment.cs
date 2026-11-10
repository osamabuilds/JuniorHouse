using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Shipping.Domain.Bookings;

/// <summary>One status the courier reported (SHPG.SHPG_EVNT): kept even when it did not move the shipment, so the history is complete (TRK-4, TRK-13).</summary>
public sealed class ShipmentEvent : Entity<long>, IAuditable
{
    private ShipmentEvent()
    {
        CourierEventId = string.Empty;
        InsrBy = string.Empty;
    }

    internal ShipmentEvent(short statusId, string courierEventId, DateTimeOffset occurredAt, short sourceId)
    {
        StatusId = statusId;
        CourierEventId = courierEventId;
        OccurredAt = occurredAt;
        SourceId = sourceId;
        InsrBy = string.Empty;
    }

    public long ShipmentId { get; private set; }

    public short StatusId { get; private set; }

    /// <summary>The courier's own id for this event: the same id twice is the same event (TRK-3).</summary>
    public string CourierEventId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public short SourceId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>What applying a courier status did.</summary>
public enum ApplyOutcome
{
    /// <summary>The shipment moved to the new status.</summary>
    Moved,

    /// <summary>The same courier event was already recorded: nothing changed (TRK-3).</summary>
    Duplicate,

    /// <summary>The event is older than where the shipment already is: recorded, shipment unchanged (TRK-4).</summary>
    Stale,

    /// <summary>The shipment is finished (delivered, returned, cancelled) or not booked yet: nothing changed.</summary>
    Ignored,
}

/// <summary>
/// One parcel on its way to one order (SHPG.SHPG_MAIN, ADR 0031). It is booked by a courier (or by hand), carries the courier's waybill, and moves along
/// the journey only forwards (TRK-4). It holds no customer data: the address and phone are read from Orders when a booking or a label needs them
/// (SEC-25). A repeated step changes nothing (BKG-5, TRK-3).
/// </summary>
public sealed class Shipment : AggregateRoot<long>, IAuditable
{
    public const int MaxWaybillLength = 40;
    public const int MaxNoteLength = 100;

    private readonly List<ShipmentEvent> _events = [];

    private Shipment()
    {
        OrderNumber = string.Empty;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public string OrderNumber { get; private set; }

    public short StatusId { get; private set; } = ShipmentStatuses.PendingBooking;

    public DateTimeOffset StatusAt { get; private set; }

    public short? CourierId { get; private set; }

    public string? Waybill { get; private set; }

    public short? BookingSourceId { get; private set; }

    public DateTimeOffset? BookedAt { get; private set; }

    public decimal CashToCollect { get; private set; }

    public int WeightGrams { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>One of <see cref="BookingErrors"/>, or null.</summary>
    public string? LastError { get; private set; }

    /// <summary>A short cleaned reason from the courier for a refusal, or null.</summary>
    public string? LastNote { get; private set; }

    /// <summary>When the worker should try again; null when no automatic try is due (booked, or waiting for staff).</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>The automatic tries are used up, or the courier refused: only staff can move it on (MBQ-6, MBQ-8).</summary>
    public bool NeedsStaff { get; private set; }

    public int FailedAttempts { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>Where the cash of this parcel stands (FR-SC-17): NotCod when there is nothing to collect, else Awaiting until a courier statement settles it.</summary>
    public short CodStatusId { get; private set; } = CodStatuses.NotCod;

    /// <summary>When the daily job flagged the unpaid delivered parcel as overdue (COD-11); cleared on settlement.</summary>
    public DateTimeOffset? CodOverdueAt { get; private set; }

    public long? ManifestId { get; private set; }

    /// <summary>How many times the label was printed: a second print is a reprint (LBL-5).</summary>
    public int LabelPrintCount { get; private set; }

    /// <summary>When the polling worker last asked the courier (TRK-6); null until it has.</summary>
    public DateTimeOffset? PolledAt { get; private set; }

    public IReadOnlyList<ShipmentEvent> Events => _events;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsBooked => Waybill is not null;

    public static Shipment Create(long orderId, string orderNumber, decimal cashToCollect, int weightGrams, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(orderNumber) || cashToCollect < 0 || weightGrams <= 0)
        {
            throw new DomainException("A shipment needs an order number, a weight and a non-negative amount to collect.");
        }

        return new Shipment
        {
            OrderId = orderId,
            OrderNumber = orderNumber,
            CashToCollect = cashToCollect,
            WeightGrams = weightGrams,
            StatusAt = at,
            NextAttemptAt = at,
            CodStatusId = cashToCollect > 0 ? CodStatuses.Awaiting : CodStatuses.NotCod,
        };
    }

    // ---------------------------------------------------------------- booking (BKG-2 to BKG-6, MBQ-2, 6, 8)

    /// <summary>A courier or staff gave a waybill. A shipment already booked keeps its first waybill (BKG-5). Returns whether it was booked now.</summary>
    public bool MarkBooked(short courierId, string waybill, short sourceId, DateTimeOffset at)
    {
        var clean = (waybill ?? string.Empty).Trim().ToUpperInvariant();
        if (clean.Length is 0 or > MaxWaybillLength || !clean.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new DomainException("A waybill is 1 to 40 letters, digits or dashes.");
        }

        if (StatusId != ShipmentStatuses.PendingBooking)
        {
            return false;
        }

        CourierId = courierId;
        Waybill = clean;
        BookingSourceId = sourceId;
        BookedAt = at;
        StatusId = ShipmentStatuses.Booked;
        StatusAt = at;
        NextAttemptAt = null;
        NeedsStaff = false;
        LastError = null;
        LastNote = null;
        Raise(new ShipmentBookedEvent(OrderId, OrderNumber, Couriers.CodeOf(courierId), clean));
        Raise(new ShipmentStatusChangedEvent(OrderId, OrderNumber, ShipmentStatusCodes.Booked, clean));
        return true;
    }

    /// <summary>
    /// Both couriers failed. An outage is tried again later, up to <paramref name="maxAttempts"/> times, then waits for staff; a refusal waits for staff at
    /// once and is not tried again blindly (BKG-6, MBQ-8).
    /// </summary>
    public void RecordBookingFailure(string error, string? note, DateTimeOffset at, int retryMinutes, int maxAttempts)
    {
        if (StatusId != ShipmentStatuses.PendingBooking)
        {
            return;
        }

        Attempts++;
        LastError = error;
        LastNote = CleanNote(note);
        if (error == BookingErrors.Rejected || Attempts >= maxAttempts)
        {
            NeedsStaff = true;
            NextAttemptAt = null;
        }
        else
        {
            NextAttemptAt = at.AddMinutes(retryMinutes);
        }
    }

    /// <summary>Staff pressed "try again now" (MBQ-3): the shipment is due immediately, and the attempt counter starts again.</summary>
    public bool RequestRetry(DateTimeOffset at)
    {
        if (StatusId != ShipmentStatuses.PendingBooking)
        {
            return false;
        }

        NeedsStaff = false;
        Attempts = 0;
        NextAttemptAt = at;
        return true;
    }

    // ---------------------------------------------------------------- cancel (BKG-10)

    /// <summary>The order was cancelled. Only a shipment the courier has not collected can be cancelled. Returns true when it was booked (the courier must be told).</summary>
    public (bool Cancelled, bool WasBooked) Cancel(DateTimeOffset at)
    {
        if (StatusId is not (ShipmentStatuses.PendingBooking or ShipmentStatuses.Booked))
        {
            return (false, false);
        }

        var wasBooked = StatusId == ShipmentStatuses.Booked;
        StatusId = ShipmentStatuses.Cancelled;
        StatusAt = at;
        NextAttemptAt = null;
        NeedsStaff = false;
        ManifestId = null;
        Raise(new ShipmentStatusChangedEvent(OrderId, OrderNumber, ShipmentStatusCodes.Cancelled, Waybill ?? string.Empty));
        return (true, wasBooked);
    }

    // ---------------------------------------------------------------- tracking (TRK-1, 3, 4, 8, 12)

    /// <summary>
    /// A courier reported a status. The event is always stored once (by the courier's id). The shipment moves only forward in rank (TRK-4), a failed
    /// attempt may be followed by a re-attempt (same rank, later time), and a finished shipment never changes.
    /// </summary>
    public ApplyOutcome Apply(short statusId, string courierEventId, DateTimeOffset occurredAt, short sourceId)
    {
        if (!ShipmentStatuses.IsKnown(statusId) || statusId is ShipmentStatuses.PendingBooking or ShipmentStatuses.Booked or ShipmentStatuses.Cancelled)
        {
            throw new DomainException("A courier can report pick-up, transit, out for delivery, a failed attempt, delivery or return.");
        }

        var id = (courierEventId ?? string.Empty).Trim();
        if (id.Length is 0 or > 80)
        {
            throw new DomainException("A courier event needs an id of up to 80 characters.");
        }

        if (_events.Any(e => e.CourierEventId == id))
        {
            return ApplyOutcome.Duplicate;
        }

        if (!IsBooked || ShipmentStatuses.IsFinal(StatusId))
        {
            return ApplyOutcome.Ignored;
        }

        _events.Add(new ShipmentEvent(statusId, id, occurredAt, sourceId));
        var newRank = ShipmentStatuses.RankOf(statusId);
        var oldRank = ShipmentStatuses.RankOf(StatusId);
        var moves = newRank > oldRank || (newRank == oldRank && statusId != StatusId && occurredAt >= StatusAt);
        if (!moves)
        {
            return ApplyOutcome.Stale;
        }

        StatusId = statusId;
        StatusAt = occurredAt;
        if (statusId == ShipmentStatuses.FailedAttempt)
        {
            FailedAttempts++;
        }

        if (statusId == ShipmentStatuses.Delivered)
        {
            DeliveredAt = occurredAt;
        }

        Raise(new ShipmentStatusChangedEvent(OrderId, OrderNumber, ShipmentStatuses.CodeOf(statusId), Waybill!));
        return ApplyOutcome.Moved;
    }

    // ---------------------------------------------------------------- manifest (MNF-3)

    public void PutOnManifest(long manifestId)
    {
        if (!IsBooked || StatusId != ShipmentStatuses.Booked)
        {
            throw new DomainException("Only a booked parcel that the courier has not collected can go on a manifest.");
        }

        if (ManifestId is not null && ManifestId != manifestId)
        {
            throw new DomainException("This parcel is already on another manifest.");
        }

        ManifestId = manifestId;
    }

    public void TakeOffManifest() => ManifestId = null;

    public void RecordLabelPrinted()
    {
        if (!IsBooked)
        {
            throw new DomainException("There is no label before the parcel is booked.");
        }

        LabelPrintCount++;
    }

    public void RecordPolled(DateTimeOffset at) => PolledAt = at;

    // ---------------------------------------------------------------- COD (COD-1, 2, 3, 11, 12)

    /// <summary>A statement line was judged. Only an awaiting parcel changes: a matched line settles it, a short or over line marks it so (COD-2, COD-3).</summary>
    public void ApplyRemittance(short outcomeId)
    {
        if (CodStatusId != CodStatuses.Awaiting)
        {
            return;
        }

        var next = outcomeId switch
        {
            RemittanceOutcomes.Matched => CodStatuses.Settled,
            RemittanceOutcomes.ShortPaid => CodStatuses.ShortPaid,
            RemittanceOutcomes.Over => CodStatuses.Over,
            _ => CodStatuses.Awaiting,
        };
        if (next != CodStatuses.Awaiting)
        {
            CodStatusId = next;
            CodOverdueAt = null;
        }
    }

    /// <summary>Staff resolved the short or over line: the parcel is closed as resolved (COD-12).</summary>
    public void ResolveCod()
    {
        if (CodStatusId is CodStatuses.ShortPaid or CodStatuses.Over)
        {
            CodStatusId = CodStatuses.Resolved;
        }
    }

    /// <summary>The daily job: a delivered parcel still awaiting cash after the cycle is flagged once (COD-11). Returns whether it was flagged now.</summary>
    public bool FlagCodOverdue(DateTimeOffset now, int cycleDays)
    {
        if (CodStatusId != CodStatuses.Awaiting || DeliveredAt is not { } delivered || CodOverdueAt is not null || delivered.AddDays(cycleDays) > now)
        {
            return false;
        }

        CodOverdueAt = now;
        return true;
    }

    private static string? CleanNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var clean = new string(note.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '.' or ',' or '-' or '_').ToArray()).Trim();
        return clean.Length == 0 ? null : clean.Length > MaxNoteLength ? clean[..MaxNoteLength] : clean;
    }
}
