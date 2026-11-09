using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Orders.Domain.Orders;

/// <summary>ORDR.COD_OTCM_LKP: how a cash on delivery confirmation attempt ended.</summary>
public static class CodOutcomes
{
    /// <summary>Confirmed by staff on the phone.</summary>
    public const short Confirmed = 1;
    public const short NoAnswer = 2;
    public const short Refused = 3;
    public const short ConfirmedByCustomerOnWhatsApp = 4;
    public const short ConfirmedByCustomerOnPage = 5;
    public const short CancelledByCustomerOnWhatsApp = 6;
    public const short CancelledByCustomerOnPage = 7;

    public static bool IsKnown(short id) => id is >= Confirmed and <= CancelledByCustomerOnPage;

    public static bool IsConfirmation(short id) => id is Confirmed or ConfirmedByCustomerOnWhatsApp or ConfirmedByCustomerOnPage;

    public static bool IsCustomerCancellation(short id) => id is CancelledByCustomerOnWhatsApp or CancelledByCustomerOnPage;

    public static string CodeOf(short id) => id switch
    {
        Confirmed => "CONF",
        NoAnswer => "NOAN",
        Refused => "REFU",
        ConfirmedByCustomerOnWhatsApp => "CWAP",
        ConfirmedByCustomerOnPage => "CWEB",
        CancelledByCustomerOnWhatsApp => "CCAN",
        CancelledByCustomerOnPage => "CWCN",
        _ => throw new DomainException($"Unknown confirmation outcome {id}."),
    };
}

public sealed class CodOutcomeLookup : OrdersLookup;

/// <summary>
/// The timing rules of cash on delivery confirmation, pure and configurable (COD-9, FR-32): the first call is due after the first delay, a
/// no-answer schedules the next attempt after the gap, and after the last allowed no-answer there is a final wait, during which the customer
/// can still confirm themselves, before the order is cancelled. Nothing here blocks anything (COD-17).
/// </summary>
public sealed record CodPolicy(TimeSpan FirstCallDelay, TimeSpan RetryGap, int MaxAttempts, TimeSpan FinalWait, TimeSpan WhatsAppConfirmAfter)
{
    public static CodPolicy Default { get; } = new(TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(120), 2, TimeSpan.FromHours(24), TimeSpan.FromMinutes(60));

    public DateTimeOffset FirstCallDue(DateTimeOffset placedAt) => placedAt + FirstCallDelay;

    /// <summary>What follows the <paramref name="attemptNumber"/>th unanswered call: the next call, or (after the last) the time of the automatic cancel.</summary>
    public (DateTimeOffset? NextCallDue, DateTimeOffset? AutoCancelAt) AfterNoAnswer(int attemptNumber, DateTimeOffset at) =>
        attemptNumber >= MaxAttempts ? (null, at + FinalWait) : (at + RetryGap, null);

    /// <summary>True when the WhatsApp request is due: the customer opted in, has not been asked, and the wait since placing has passed.</summary>
    public bool WhatsAppRequestDue(bool optedIn, DateTimeOffset? alreadyAskedAt, DateTimeOffset placedAt, DateTimeOffset now) =>
        optedIn && alreadyAskedAt is null && now >= placedAt + WhatsAppConfirmAfter;
}

/// <summary>One confirmation attempt (ORDR.COD_ATMP): the number, how it ended, who, when. Append-only in practice (COD-8).</summary>
public sealed class CodAttempt : Entity<long>, IAuditable
{
    private CodAttempt()
    {
        InsrBy = string.Empty;
    }

    internal CodAttempt(long orderId, int number, short outcomeId, short actorKindId, string? actorRef, string? note, DateTimeOffset at)
    {
        OrderId = orderId;
        Number = number;
        OutcomeId = outcomeId;
        ActorKindId = actorKindId;
        ActorRef = actorRef;
        Note = note is { Length: > OrderHistory.MaxNoteLength } ? note[..OrderHistory.MaxNoteLength] : note;
        At = at;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public int Number { get; private set; }

    public short OutcomeId { get; private set; }

    public short ActorKindId { get; private set; }

    public string? ActorRef { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset At { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// Refusals per mobile number (ORDR.COD_RISK, COD-4, COD-5, COD-10): counts only, so staff can see a pattern. Reaching the threshold sets a
/// flag that staff see in the queue; it never blocks an order (owner decision). A customer cancelling on WhatsApp or the page is not a refusal (COD-15).
/// </summary>
public sealed class CodRisk : Entity<long>, IAuditable
{
    private CodRisk()
    {
        Mobile = string.Empty;
        InsrBy = string.Empty;
    }

    public CodRisk(string mobile)
    {
        Mobile = mobile;
        InsrBy = string.Empty;
    }

    public string Mobile { get; private set; }

    public int RefusalCount { get; private set; }

    public int NoAnswerCancelCount { get; private set; }

    public bool IsFlagged { get; private set; }

    public string? ClearedBy { get; private set; }

    public DateTimeOffset? ClearedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void NoteRefusal(int threshold)
    {
        RefusalCount++;
        if (RefusalCount >= threshold)
        {
            IsFlagged = true;
            ClearedBy = null;
            ClearedAt = null;
        }
    }

    public void NoteNoAnswerCancel() => NoAnswerCancelCount++;

    /// <summary>Staff clear the flag after review (COD-10); the counts stay as history.</summary>
    public void Clear(string staffRef, DateTimeOffset at)
    {
        IsFlagged = false;
        ClearedBy = staffRef;
        ClearedAt = at;
    }
}
