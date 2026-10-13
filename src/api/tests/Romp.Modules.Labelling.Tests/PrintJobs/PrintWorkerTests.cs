using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Infrastructure.Persistence;
using Romp.Modules.Labelling.Domain.PrintJobs;
using Romp.Modules.Labelling.Tests.Support;
using ValidationException = Romp.BuildingBlocks.Application.ValidationException;

namespace Romp.Modules.Labelling.Tests.PrintJobs;

[Collection(LabellingPostgresFixtureDefinition.Name)]
public sealed class PrintWorkerTests(LabellingPostgres postgres)
{
    private const string GoodPrinter = "FAKE-PDF";
    private const string JamPrinter = "FAKE-JAM";

    /// <summary>A style with one accepted work item of <paramref name="qty"/> tags to print.</summary>
    private static async Task<(long ItemId, long VariantId, long StyleId)> ReadyItemAsync(LabellingHost host, int qty = 10, bool concession = false)
    {
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(concession ? LabelOutcome.Concession : LabelOutcome.Accepted, poId, (1, 10, qty))));
        var item = await host.InScopeAsync(sp => sp.GetRequiredService<SkuDbContext>().WorkItems.AsNoTracking().SingleAsync(i => i.PoId == poId));
        return (item.Id, item.VariantId!.Value, styleId);
    }

    /// <summary>The database is shared by every test in the collection, so a test that runs the worker first clears anything earlier tests left waiting.</summary>
    private static async Task CleanQueueAsync(LabellingPostgres postgres)
    {
        await using var db = postgres.NewContext();
        await db.Database.ExecuteSqlRawAsync("""UPDATE "SKU"."PRT_JOB" SET "PRT_JOB_STS_ID" = {0} WHERE "PRT_JOB_STS_ID" IN ({1}, {2});""", PrintJobStatus.Cancelled, PrintJobStatus.Queued, PrintJobStatus.Printing);
    }

    private static async Task<int> RunWorkerAsync(LabellingHost host) =>
        await host.InScopeAsync(sp => sp.GetRequiredService<PrintJobProcessor>().RunOnceAsync(default));

    private static Task<PrintJob> JobAsync(LabellingPostgres postgres, long id) =>
        postgres.NewContext().PrintJobs.Include(j => j.Attempts).AsNoTracking().SingleAsync(j => j.Id == id);

    [Fact]
    [Trait("Spec", "PJ-1")]
    public async Task QueueingAJob_SavesItInstantly_WithoutContactingAPrinter()
    {
        using var host = new LabellingHost(postgres);
        var (itemId, _, _) = await ReadyItemAsync(host);

        var job = await host.SendAsync(new QueueTagsCommand(itemId, 4, null, "Asha"));

        Assert.Equal("Queued", job.StatusCode);
        Assert.Matches(@"^PJ-2026-\d{5}$", job.JobNo);
        Assert.Equal(1299m, job.PriceSnapshot);
        Assert.Equal(GoodPrinter, job.PrinterCode);
        Assert.Equal("Asha", job.RequestedBy);
        Assert.Empty(host.Printer.Sent);
    }

    [Fact]
    [Trait("Spec", "PJ-10")]
    public async Task APrintedJob_RecordsTheAttempt_FinishesTheLabelledCount_AndRaisesTheEvent()
    {
        await CleanQueueAsync(postgres);
        using var host = new LabellingHost(postgres);
        var (itemId, _, _) = await ReadyItemAsync(host);
        var queued = await host.SendAsync(new QueueTagsCommand(itemId, 10, null, "Asha"));

        await RunWorkerAsync(host);

        var job = await JobAsync(postgres, queued.Id);
        Assert.Equal(PrintJobStatus.Printed, job.StatusId);
        var attempt = Assert.Single(job.Attempts);
        Assert.Equal(AttemptOutcome.Printed, attempt.OutcomeId);
        Assert.Equal(1, job.AttemptCount);
        var sent = Assert.Single(host.Printer.Sent);
        Assert.Equal(GoodPrinter, sent.PrinterCode);
        Assert.Equal("application/pdf", sent.Document.ContentType);

        var row = await host.SendAsync(new GetLabelWorkItemQuery(itemId));
        Assert.Equal(10, row.PrintedQty);
        await using var db = postgres.NewContext();
        Assert.Equal(1, db.Set<OutboxMessage>().Where(m => m.EventType == "TagJobPrintedEvent").AsEnumerable().Count(m => m.Payload.Contains(queued.JobNo, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Spec", "PJ-2")]
    [Trait("Spec", "PJ-3")]
    public async Task AJammedPrinter_RetriesWithBackoff_EndsFailed_AndStaffCanRetryThatSameJob()
    {
        await CleanQueueAsync(postgres);
        using var host = new LabellingHost(postgres);
        host.Printer.FailingPrinters.Add(GoodPrinter);
        var (itemId, _, _) = await ReadyItemAsync(host);
        var queued = await host.SendAsync(new QueueTagsCommand(itemId, 5, null, "Asha"));

        await RunWorkerAsync(host); // attempt 1
        var afterFirst = await JobAsync(postgres, queued.Id);
        Assert.Equal(PrintJobStatus.Queued, afterFirst.StatusId);
        Assert.Equal(host.Time.Now.AddSeconds(10), afterFirst.NextAttemptAt);

        Assert.Equal(0, await RunWorkerAsync(host)); // not due yet: the worker moves on instead of hammering the printer

        host.Time.Now = host.Time.Now.AddSeconds(11);
        await RunWorkerAsync(host); // attempt 2
        host.Time.Now = host.Time.Now.AddSeconds(21);
        await RunWorkerAsync(host); // attempt 3 = the limit

        var failed = await JobAsync(postgres, queued.Id);
        Assert.Equal(PrintJobStatus.Failed, failed.StatusId);
        Assert.Equal("Paper jam", failed.LastError);
        Assert.Equal(3, failed.Attempts.Count(a => a.OutcomeId == AttemptOutcome.Failed));

        // PJ-3: Retry is the same job, attempts reset, no new job or GTIN.
        host.Printer.FailingPrinters.Clear();
        await host.SendAsync(new RetryPrintJobCommand(queued.Id, "Asha"));
        await RunWorkerAsync(host);
        var printed = await JobAsync(postgres, queued.Id);
        Assert.Equal(PrintJobStatus.Printed, printed.StatusId);
        Assert.Equal("Asha", printed.ManualActionBy);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.PrintJobs.CountAsync(j => j.WorkItemId == itemId));
    }

    [Fact]
    [Trait("Spec", "PJ-4")]
    public async Task AJammedPrinter_DoesNotStopTheOtherPrintersJobs()
    {
        await CleanQueueAsync(postgres);
        using var host = new LabellingHost(postgres);
        host.Printer.FailingPrinters.Add(JamPrinter);
        var (item1, _, _) = await ReadyItemAsync(host);
        var (item2, _, _) = await ReadyItemAsync(host);
        var stuck = await host.SendAsync(new QueueTagsCommand(item1, 3, JamPrinter, "Asha"));
        var fine = await host.SendAsync(new QueueTagsCommand(item2, 3, GoodPrinter, "Asha"));

        await RunWorkerAsync(host);

        Assert.Equal(PrintJobStatus.Queued, (await JobAsync(postgres, stuck.Id)).StatusId);
        Assert.Equal(PrintJobStatus.Printed, (await JobAsync(postgres, fine.Id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PJ-9")]
    public async Task EightWorkersRacingForOneJob_ExactlyOneWinsTheClaim()
    {
        using var host = new LabellingHost(postgres);
        var (itemId, _, _) = await ReadyItemAsync(host);
        var queued = await host.SendAsync(new QueueTagsCommand(itemId, 2, GoodPrinter, "Asha"));
        // Make sure only this job is claimable on the printer: park everything else that earlier tests may have left queued.
        await using (var db = postgres.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                """UPDATE "SKU"."PRT_JOB" SET "PRT_JOB_STS_ID" = {0} WHERE "PRT_JOB_STS_ID" = {1} AND "ID" <> {2};""",
                PrintJobStatus.Cancelled, PrintJobStatus.Queued, queued.Id);
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            host.InScopeAsync(sp => sp.GetRequiredService<IPrintJobClaims>().ClaimNextAsync(1, host.Time.Now, default))));

        Assert.Equal(queued.Id, Assert.Single(claims.OfType<long>()));
        var job = await JobAsync(postgres, queued.Id);
        Assert.Equal(PrintJobStatus.Printing, job.StatusId);
        Assert.Equal(1, job.AttemptCount);
    }

    [Fact]
    [Trait("Spec", "PJ-9")]
    public async Task AJobStuckInPrinting_GoesBackToTheQueue()
    {
        using var host = new LabellingHost(postgres);
        var (itemId, _, _) = await ReadyItemAsync(host);
        var queued = await host.SendAsync(new QueueTagsCommand(itemId, 2, GoodPrinter, "Asha"));
        await using (var db = postgres.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                """UPDATE "SKU"."PRT_JOB" SET "PRT_JOB_STS_ID" = {0}, "UPDT_DTE" = {1} WHERE "ID" = {2};""",
                PrintJobStatus.Printing, host.Time.Now.AddMinutes(-30), queued.Id);
        }

        var recovered = await host.InScopeAsync(sp => sp.GetRequiredService<IPrintJobClaims>().RecoverStuckAsync(host.Time.Now, TimeSpan.FromMinutes(5), default));

        Assert.True(recovered >= 1);
        Assert.Equal(PrintJobStatus.Queued, (await JobAsync(postgres, queued.Id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PJ-8")]
    public async Task AGtinVoidedAfterQueueing_IsNeverPrinted()
    {
        await CleanQueueAsync(postgres);
        using var host = new LabellingHost(postgres);
        var (itemId, variantId, _) = await ReadyItemAsync(host);
        var queued = await host.SendAsync(new QueueTagsCommand(itemId, 3, GoodPrinter, "Asha"));
        await host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Recalled, null, "Asha"));

        await RunWorkerAsync(host);

        Assert.DoesNotContain(host.Printer.Sent, s => s.Document.SkuCode.Contains(queued.SkuCode, StringComparison.Ordinal));
        var job = await JobAsync(postgres, queued.Id);
        Assert.NotEqual(PrintJobStatus.Printed, job.StatusId);
        Assert.Contains("voided", job.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "PJ-8")]
    public async Task QueueingOrReprinting_ForARetiredGtin_IsRefused()
    {
        using var host = new LabellingHost(postgres);
        var (itemId, variantId, _) = await ReadyItemAsync(host);
        await host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Error, null, "Asha"));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new QueueTagsCommand(itemId, 1, null, "Asha")));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ReprintTagCommand(variantId, 1, ReprintReasons.Lost, null, null, "Asha")));
    }

    [Fact]
    [Trait("Spec", "PJ-5")]
    [Trait("Spec", "PJ-6")]
    public async Task AReprint_UsesTheSameGtin_RecordsWhyAndWho_AndKeepsTheConcessionMark()
    {
        await CleanQueueAsync(postgres);
        using var host = new LabellingHost(postgres);
        var (itemId, variantId, _) = await ReadyItemAsync(host, concession: true);
        var original = await host.SendAsync(new QueueTagsCommand(itemId, 10, null, "Asha"));
        await RunWorkerAsync(host);

        var reprint = await host.SendAsync(new ReprintTagCommand(variantId, 2, ReprintReasons.Damaged, "ripped in the bin", null, "Bilal"));
        await RunWorkerAsync(host);

        await using (var check = postgres.NewContext())
        {
            Assert.Equal(LabelOutcome.Concession, (await check.PrintJobs.SingleAsync(j => j.Id == reprint.Id)).OutcomeId);
        }

        Assert.Equal("Reprint", reprint.KindCode);
        Assert.Equal(original.VariantGtinId, reprint.VariantGtinId);
        Assert.Equal(original.Gtin, reprint.Gtin);
        Assert.Equal("Damaged", reprint.ReprintReasonCode);
        Assert.Equal("ripped in the bin", reprint.Note);
        Assert.Equal("Bilal", reprint.RequestedBy);

        var history = await host.SendAsync(new GetPrintHistoryQuery(variantId));
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(["Reprint", "Original"], history.Items.Select(i => i.KindCode));
        // Same variant, same number: only a single GTIN row exists, so no new barcode was ever generated.
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Gtins.CountAsync(g => g.VariantId == variantId));
        // The reprinted document still carries the 2ND mark because the stock is concession stock.
        var zpl = await host.InScopeAsync(sp => sp.GetRequiredService<ITagDocumentBuilder>().BuildAsync(new TagRequest(original.VariantGtinId, 1, LabelOutcome.Concession, 1299m), PrintFormat.Zpl, default));
        Assert.Contains("2ND", Encoding.UTF8.GetString(zpl.Content), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PJ-7")]
    public async Task Reprint_NeedsAReasonAPersonAndASaneCount()
    {
        using var host = new LabellingHost(postgres);
        var (_, variantId, _) = await ReadyItemAsync(host);

        var noReason = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 1, 0, null, null, "Asha")));
        Assert.True(noReason.Errors.ContainsKey("ReasonId"));
        var noPerson = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 1, ReprintReasons.Lost, null, null, "")));
        Assert.True(noPerson.Errors.ContainsKey("ActingPerson"));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 0, ReprintReasons.Lost, null, null, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 5_001, ReprintReasons.Lost, null, null, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 1, ReprintReasons.Lost, new string('n', 201), null, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 1, ReprintReasons.Lost, "line\r\nbreak", null, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ReprintTagCommand(variantId, 1, ReprintReasons.Lost, null, null, "Asha\nForged: line")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new ReprintTagCommand(long.MaxValue, 1, ReprintReasons.Lost, null, null, "Asha")));
    }

    [Fact]
    [Trait("Spec", "PJ-12")]
    public async Task APrinterIsChosenByConfiguredCodeOnly_AnUnknownOneIsRefused()
    {
        using var host = new LabellingHost(postgres);
        var (itemId, _, _) = await ReadyItemAsync(host);

        foreach (var attempt in new[] { "192.168.1.5:9100", "evil.example:9100", "NO-SUCH", "FAKE-PDF'; DROP TABLE x;--" })
        {
            var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new QueueTagsCommand(itemId, 1, attempt, "Asha")));
            Assert.True(ex.Errors.ContainsKey("PrinterCode"), attempt);
        }

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new QueueTagsCommand(itemId, 1, new string('x', 31), "Asha")));
    }

    [Fact]
    [Trait("Spec", "PJ-11")]
    public async Task APrinterQueueHasALimit()
    {
        using var host = new LabellingHost(postgres, o => o.MaxQueuedJobsPerPrinter = 2);
        await using (var db = postgres.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("""UPDATE "SKU"."PRT_JOB" SET "PRT_JOB_STS_ID" = {0} WHERE "PRT_JOB_STS_ID" = {1};""", PrintJobStatus.Cancelled, PrintJobStatus.Queued);
        }

        var (itemId, _, _) = await ReadyItemAsync(host, qty: 20);
        await host.SendAsync(new QueueTagsCommand(itemId, 1, JamPrinter, "Asha"));
        await host.SendAsync(new QueueTagsCommand(itemId, 1, JamPrinter, "Asha"));

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new QueueTagsCommand(itemId, 1, JamPrinter, "Asha")));
        Assert.Contains("limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        await host.SendAsync(new QueueTagsCommand(itemId, 1, GoodPrinter, "Asha")); // another printer is unaffected
    }

    [Fact]
    [Trait("Spec", "PJ-3")]
    public async Task Cancel_WorksOnQueuedAndFailedJobs_NotOnPrintedOnes()
    {
        await CleanQueueAsync(postgres);
        using var host = new LabellingHost(postgres);
        var (itemId, _, _) = await ReadyItemAsync(host);
        var queued = await host.SendAsync(new QueueTagsCommand(itemId, 2, GoodPrinter, "Asha"));
        var cancelled = await host.SendAsync(new CancelPrintJobCommand(queued.Id, "Bilal"));
        Assert.Equal("Cancelled", cancelled.StatusCode);
        Assert.Equal("Bilal", cancelled.ManualActionBy);

        var second = await host.SendAsync(new QueueTagsCommand(itemId, 2, GoodPrinter, "Asha"));
        await RunWorkerAsync(host);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CancelPrintJobCommand(second.Id, "Bilal")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new RetryPrintJobCommand(long.MaxValue, "Bilal")));
    }

    [Fact]
    [Trait("Spec", "PJ-13")]
    public async Task TheQueueScreen_IsPaged_FilteredByStatus_AndRunsAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new LabellingHost(postgres, counter: counter);
        var (itemId, _, _) = await ReadyItemAsync(host, qty: 50);
        for (var i = 0; i < 6; i++)
        {
            await host.SendAsync(new QueueTagsCommand(itemId, 1, JamPrinter, "Asha"));
        }

        counter.Reset();
        var page1 = await host.SendAsync(new SearchPrintJobsQuery("queued", null, 1, 4));
        var queries = counter.Count;
        var page2 = await host.SendAsync(new SearchPrintJobsQuery("queued", null, 2, 4));

        Assert.Equal(4, page1.Items.Count);
        Assert.True(page1.TotalCount >= 6);
        Assert.True(queries <= 2, $"a page of the queue must not run per row: {queries} queries");
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
        Assert.True(page1.Items.Zip(page1.Items.Skip(1)).All(p => p.First.Id > p.Second.Id));
        Assert.All(page1.Items, i => Assert.Equal("Queued", i.StatusCode));
        var clamped = await host.SendAsync(new SearchPrintJobsQuery(null, null, 1, 9_999));
        Assert.Equal(PageRequest.MaxPageSize, clamped.PageSize);
        Assert.Equal(2, (await host.SendAsync(new ListPrintersQuery())).Count);
    }

    [Fact]
    [Trait("Spec", "TAG-1")]
    [Trait("Spec", "TAG-3")]
    public async Task TheTagBuilder_PutsTheCurrentCatalogPriceAndTheSkuOnTheZpl()
    {
        using var host = new LabellingHost(postgres);
        var (itemId, _, styleId) = await ReadyItemAsync(host);
        var row = await host.SendAsync(new GetLabelWorkItemQuery(itemId));
        var job = await host.SendAsync(new QueueTagsCommand(itemId, 3, null, "Asha"));

        var zpl = await host.InScopeAsync(sp => sp.GetRequiredService<ITagDocumentBuilder>().BuildAsync(new TagRequest(job.VariantGtinId, 3, LabelOutcome.Accepted, 1299m), PrintFormat.Zpl, default));
        var text = Encoding.UTF8.GetString(zpl.Content);

        Assert.Contains($"Style {styleId}", text, StringComparison.Ordinal);
        Assert.Contains("Rs. 1,299", text, StringComparison.Ordinal);
        Assert.Contains($"^BEN,64,Y,N^FD{row.Gtin![..12]}^FS", text, StringComparison.Ordinal);
        Assert.Contains("^PQ3,0,1,N", text, StringComparison.Ordinal);
        Assert.Equal(row.SkuCode, zpl.SkuCode);
        var preview = await host.SendAsync(new GetTagPreviewQuery(itemId));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(preview.Content, 0, 4), StringComparison.Ordinal);
        Assert.EndsWith(".pdf", preview.FileName, StringComparison.Ordinal);
    }
}
