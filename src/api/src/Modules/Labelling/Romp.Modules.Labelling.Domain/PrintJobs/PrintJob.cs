using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Contracts;

namespace Romp.Modules.Labelling.Domain.PrintJobs;

/// <summary>
/// A request to print tags, saved before any printer is contacted (ADR 0010). State machine:
/// Queued → Printing → Printed | Queued (retry with backoff) | Failed; Failed → Queued (manual retry); Queued/Failed → Cancelled.
/// The Queued → Printing step is a single conditional UPDATE in the worker (so two workers can never claim one job); everything
/// after it is these methods. Maps to SKU.PRT_JOB.
/// </summary>
public sealed class PrintJob : AggregateRoot<long>, IAuditable
{
    public const int MaxNoteLength = 200;
    public const int MaxErrorLength = 500;

    private readonly List<PrintAttempt> _attempts = [];

    private PrintJob()
    {
        JobNo = string.Empty;
        RequestedBy = string.Empty;
    }

    private PrintJob(
        string jobNo, short kindId, long variantGtinId, long? workItemId, int qty, short outcomeId, decimal priceSnapshot,
        short printerId, string requestedBy, DateTimeOffset now)
    {
        if (qty <= 0)
        {
            throw new DomainException("A print job needs a positive number of tags.");
        }

        if (priceSnapshot < 0)
        {
            throw new DomainException("The tag price cannot be negative.");
        }

        JobNo = jobNo;
        KindId = kindId;
        VariantGtinId = variantGtinId;
        WorkItemId = workItemId;
        Qty = qty;
        OutcomeId = outcomeId;
        PriceSnapshot = priceSnapshot;
        PrinterId = printerId;
        RequestedBy = requestedBy;
        StatusId = PrintJobStatus.Queued;
        NextAttemptAt = now;
    }

    public string JobNo { get; private set; }

    public short KindId { get; private set; }

    public long VariantGtinId { get; private set; }

    public long? WorkItemId { get; private set; }

    public int Qty { get; private set; }

    public short OutcomeId { get; private set; }

    public decimal PriceSnapshot { get; private set; }

    public short PrinterId { get; private set; }

    public short StatusId { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    public short? ReprintReasonId { get; private set; }

    public string? Note { get; private set; }

    public string RequestedBy { get; private set; }

    /// <summary>The person behind the latest manual action (retry or cancel), for the audit trail (NFR-SC-05).</summary>
    public string? ManualActionBy { get; private set; }

    public IReadOnlyCollection<PrintAttempt> Attempts => _attempts.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static PrintJob Original(
        string jobNo, long variantGtinId, long workItemId, int qty, short outcomeId, decimal priceSnapshot,
        short printerId, string requestedBy, DateTimeOffset now) =>
        new(jobNo, PrintJobKind.Original, variantGtinId, workItemId, qty, outcomeId, priceSnapshot, printerId, requestedBy, now);

    public static PrintJob Reprint(
        string jobNo, long variantGtinId, int qty, short outcomeId, decimal priceSnapshot, short printerId,
        short reasonId, string? note, string requestedBy, DateTimeOffset now)
    {
        if (!ReprintReasons.IsKnown(reasonId))
        {
            throw new DomainException("Choose why the tag is being reprinted.");
        }

        var job = new PrintJob(jobNo, PrintJobKind.Reprint, variantGtinId, null, qty, outcomeId, priceSnapshot, printerId, requestedBy, now)
        {
            ReprintReasonId = reasonId,
            Note = note,
        };
        return job;
    }

    /// <summary>The printer confirmed the job (the worker claimed it first, so the status is Printing). Raises <see cref="TagJobPrintedEvent"/>.</summary>
    public void MarkPrinted(DateTimeOffset now, string skuCode, string gtin, string? outputReference)
    {
        EnsureStatus(PrintJobStatus.Printing, "finish");
        StatusId = PrintJobStatus.Printed;
        LastError = null;
        _attempts.Add(new PrintAttempt(AttemptCount, AttemptOutcome.Printed, null, outputReference, now));
        Raise(new TagJobPrintedEvent(Id, JobNo, skuCode, gtin, Qty, PrintJobKind.CodeOf(KindId)));
    }

    /// <summary>
    /// The printer failed (PJ-2). Below the attempt limit the job goes back to Queued with a later attempt time (growing
    /// backoff, so the worker moves on to other jobs instead of hammering a jammed printer); at the limit it becomes Failed.
    /// </summary>
    public void RecordFailure(DateTimeOffset now, string error, IReadOnlyList<TimeSpan> backoff, int maxAttempts)
    {
        EnsureStatus(PrintJobStatus.Printing, "fail");
        var trimmed = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        LastError = trimmed;
        _attempts.Add(new PrintAttempt(AttemptCount, AttemptOutcome.Failed, trimmed, null, now));

        if (AttemptCount >= maxAttempts)
        {
            StatusId = PrintJobStatus.Failed;
            return;
        }

        StatusId = PrintJobStatus.Queued;
        var delay = backoff.Count == 0 ? TimeSpan.Zero : backoff[Math.Min(AttemptCount - 1, backoff.Count - 1)];
        NextAttemptAt = now + delay;
    }

    /// <summary>Staff pressed Retry on a Failed job (PJ-3): the same job runs again, attempts reset, nothing new is created.</summary>
    public void Retry(DateTimeOffset now, string person)
    {
        EnsureStatus(PrintJobStatus.Failed, "retry");
        ManualActionBy = person;
        StatusId = PrintJobStatus.Queued;
        AttemptCount = 0;
        NextAttemptAt = now;
    }

    public void Cancel(string person)
    {
        if (StatusId is not (PrintJobStatus.Queued or PrintJobStatus.Failed))
        {
            throw new DomainException("Only a queued or failed print job can be cancelled.");
        }

        StatusId = PrintJobStatus.Cancelled;
        ManualActionBy = person;
    }

    private void EnsureStatus(short expected, string action)
    {
        if (StatusId != expected)
        {
            throw new DomainException($"Print job {JobNo} cannot {action} from its current state.");
        }
    }
}

/// <summary>One try at printing a job (history, never updated). Maps to SKU.PRT_JOB_ATMP.</summary>
public sealed class PrintAttempt : Entity<long>, IAuditable
{
    private PrintAttempt()
    {
    }

    internal PrintAttempt(int attemptNo, short outcomeId, string? error, string? outputReference, DateTimeOffset at)
    {
        AttemptNo = attemptNo;
        OutcomeId = outcomeId;
        Error = error;
        OutputReference = outputReference;
        At = at;
    }

    public long PrintJobId { get; private set; }

    public int AttemptNo { get; private set; }

    public short OutcomeId { get; private set; }

    public string? Error { get; private set; }

    public string? OutputReference { get; private set; }

    public DateTimeOffset At { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
