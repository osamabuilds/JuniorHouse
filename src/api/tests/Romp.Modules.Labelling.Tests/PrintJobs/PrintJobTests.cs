using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.PrintJobs;

namespace Romp.Modules.Labelling.Tests.PrintJobs;

public sealed class PrintJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

    /// <summary>A job as the worker sees it after claiming: Printing, attempt counter already incremented.</summary>
    private static PrintJob Claimed(int attempt = 1)
    {
        var job = PrintJob.Original("PJ-2026-00001", 5, 9, 10, LabelOutcome.Accepted, 1299m, 1, "Asha", Now);
        Set(job, nameof(PrintJob.StatusId), PrintJobStatus.Printing);
        Set(job, nameof(PrintJob.AttemptCount), attempt);
        typeof(Entity<long>).GetProperty(nameof(Entity<long>.Id))!.SetValue(job, 3L);
        return job;
    }

    private static void Set(PrintJob job, string property, object value) =>
        typeof(PrintJob).GetProperty(property)!.SetValue(job, value);

    [Fact]
    [Trait("Spec", "PJ-1")]
    public void New_IsQueuedImmediately_WithThePriceSnapshotAndRequester()
    {
        var job = PrintJob.Original("PJ-2026-00001", 5, 9, 10, LabelOutcome.Concession, 1299m, 1, "Asha", Now);

        Assert.Equal(PrintJobStatus.Queued, job.StatusId);
        Assert.Equal(Now, job.NextAttemptAt);
        Assert.Equal(1299m, job.PriceSnapshot);
        Assert.Equal("Asha", job.RequestedBy);
        Assert.Equal(PrintJobKind.Original, job.KindId);
        Assert.Equal(9, job.WorkItemId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [Trait("Spec", "PJ-7")]
    public void New_NeedsAPositiveTagCount(int qty) =>
        Assert.Throws<DomainException>(() => PrintJob.Original("PJ-1", 5, 9, qty, LabelOutcome.Accepted, 1m, 1, "Asha", Now));

    [Fact]
    [Trait("Spec", "PJ-5")]
    [Trait("Spec", "PJ-6")]
    public void Reprint_KeepsTheReasonAndNote_AndHasNoWorkItem()
    {
        var job = PrintJob.Reprint("PJ-2", 5, 3, LabelOutcome.Accepted, 1399m, 1, ReprintReasons.Damaged, "ripped in the bin", "Asha", Now);

        Assert.Equal(PrintJobKind.Reprint, job.KindId);
        Assert.Null(job.WorkItemId);
        Assert.Equal(ReprintReasons.Damaged, job.ReprintReasonId);
        Assert.Equal("ripped in the bin", job.Note);
    }

    [Fact]
    [Trait("Spec", "PJ-7")]
    public void Reprint_NeedsAKnownReason() =>
        Assert.Throws<DomainException>(() => PrintJob.Reprint("PJ-2", 5, 3, LabelOutcome.Accepted, 1m, 1, 99, null, "Asha", Now));

    [Fact]
    [Trait("Spec", "PJ-10")]
    public void MarkPrinted_FinishesTheJob_RecordsTheAttempt_AndRaisesTheEvent()
    {
        var job = Claimed();

        job.MarkPrinted(Now, "ROMP-STY-RED-2-3Y", "5901234123457", "out/abc");

        Assert.Equal(PrintJobStatus.Printed, job.StatusId);
        var attempt = Assert.Single(job.Attempts);
        Assert.Equal(AttemptOutcome.Printed, attempt.OutcomeId);
        Assert.Equal("out/abc", attempt.OutputReference);
        var raised = Assert.IsType<TagJobPrintedEvent>(Assert.Single(job.DomainEvents));
        Assert.Equal("Original", raised.KindCode);
        Assert.Equal(10, raised.Qty);
    }

    [Fact]
    [Trait("Spec", "PJ-2")]
    public void RecordFailure_BelowTheLimit_RequeuesWithGrowingBackoff()
    {
        var first = Claimed(attempt: 1);
        first.RecordFailure(Now, "Paper jam", Backoff, maxAttempts: 4);
        Assert.Equal(PrintJobStatus.Queued, first.StatusId);
        Assert.Equal(Now.AddSeconds(30), first.NextAttemptAt);
        Assert.Equal("Paper jam", first.LastError);

        var third = Claimed(attempt: 3);
        third.RecordFailure(Now, "Paper jam", Backoff, maxAttempts: 4);
        Assert.Equal(Now.AddMinutes(10), third.NextAttemptAt);

        var beyondSchedule = Claimed(attempt: 3);
        beyondSchedule.RecordFailure(Now, "x", [TimeSpan.FromSeconds(1)], maxAttempts: 4);
        Assert.Equal(Now.AddSeconds(1), beyondSchedule.NextAttemptAt);
    }

    [Fact]
    [Trait("Spec", "PJ-2")]
    public void RecordFailure_AtTheLimit_EndsFailed_WithTheLastErrorShown()
    {
        var job = Claimed(attempt: 4);

        job.RecordFailure(Now, "Printer offline", Backoff, maxAttempts: 4);

        Assert.Equal(PrintJobStatus.Failed, job.StatusId);
        Assert.Equal("Printer offline", job.LastError);
        Assert.Equal(AttemptOutcome.Failed, Assert.Single(job.Attempts).OutcomeId);
    }

    [Fact]
    [Trait("Spec", "PJ-2")]
    public void RecordFailure_TrimsTheErrorText_SoAPrinterCannotFloodTheDatabase()
    {
        var job = Claimed(attempt: 4);

        job.RecordFailure(Now, new string('x', 5_000), Backoff, maxAttempts: 4);

        Assert.Equal(PrintJob.MaxErrorLength, job.LastError!.Length);
    }

    [Fact]
    [Trait("Spec", "PJ-3")]
    public void Retry_OnAFailedJob_ResetsAttemptsAndRequeues_WithoutANewJob()
    {
        var job = Claimed(attempt: 4);
        job.RecordFailure(Now, "Printer offline", Backoff, maxAttempts: 4);

        job.Retry(Now.AddMinutes(1), "Asha");

        Assert.Equal(PrintJobStatus.Queued, job.StatusId);
        Assert.Equal(0, job.AttemptCount);
        Assert.Equal(Now.AddMinutes(1), job.NextAttemptAt);
    }

    [Fact]
    [Trait("Spec", "PJ-3")]
    public void Retry_OnAQueuedOrPrintedJob_IsRefused()
    {
        var queued = PrintJob.Original("PJ-1", 5, 9, 1, LabelOutcome.Accepted, 1m, 1, "Asha", Now);
        Assert.Throws<DomainException>(() => queued.Retry(Now, "Asha"));

        var printed = Claimed();
        printed.MarkPrinted(Now, "S", "5901234123457", null);
        Assert.Throws<DomainException>(() => printed.Retry(Now, "Asha"));
    }

    [Fact]
    [Trait("Spec", "PJ-3")]
    public void Cancel_WorksOnQueuedAndFailed_NotOnPrintingOrPrinted()
    {
        var queued = PrintJob.Original("PJ-1", 5, 9, 1, LabelOutcome.Accepted, 1m, 1, "Asha", Now);
        queued.Cancel("Asha");
        Assert.Equal(PrintJobStatus.Cancelled, queued.StatusId);

        Assert.Throws<DomainException>(() => Claimed().Cancel("Asha"));

        var printed = Claimed();
        printed.MarkPrinted(Now, "S", "5901234123457", null);
        Assert.Throws<DomainException>(() => printed.Cancel("Asha"));
    }

    [Fact]
    [Trait("Spec", "PJ-9")]
    public void MarkPrinted_OnAJobThatIsNotPrinting_IsRefused()
    {
        var queued = PrintJob.Original("PJ-1", 5, 9, 1, LabelOutcome.Accepted, 1m, 1, "Asha", Now);

        Assert.Throws<DomainException>(() => queued.MarkPrinted(Now, "S", "5901234123457", null));
        Assert.Throws<DomainException>(() => queued.RecordFailure(Now, "x", Backoff, 4));
    }
}
