using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.PrintJobs;

namespace Romp.Modules.Labelling.Application.PrintJobs;

/// <summary>A configured printer as the sending code sees it.</summary>
public sealed record PrinterTarget(short Id, string Code, short FormatId, string Address);

/// <summary>The printer's answer: success (with an optional reference to the output) or a short reason (PJ-2).</summary>
public sealed record PrintOutcome(bool Success, string? Error, string? OutputReference)
{
    public static PrintOutcome Ok(string? outputReference = null) => new(true, null, outputReference);

    public static PrintOutcome Fail(string error) => new(false, error, null);
}

/// <summary>
/// Sends one rendered document to one printer (ADR 0010). Implemented in Infrastructure (ZPL over TCP, and a PDF "fake"
/// for development). It never throws for an ordinary printer problem; it returns a failed <see cref="PrintOutcome"/>.
/// </summary>
public interface ILabelPrinter
{
    Task<PrintOutcome> SendAsync(PrinterTarget printer, LabelDocument document, CancellationToken cancellationToken);
}

/// <summary>
/// The worker's brain (PJ-2, PJ-4, PJ-9): recover stuck jobs, then for each active printer claim and print due jobs one at a time.
/// A failed job is re-queued with a later attempt time, so it never blocks the jobs behind it, and a printer that is down only
/// affects its own jobs. The claim and the completion are separate transactions; the printer I/O between them holds none.
/// </summary>
public sealed partial class PrintJobProcessor(
    IPrintJobClaims claims,
    ILabellingDbContext dbContext,
    ITagDocumentBuilder tags,
    ILabelPrinter printer,
    ISender sender,
    IOptions<LabellingOptions> options,
    TimeProvider timeProvider,
    ILogger<PrintJobProcessor> logger)
{
    private const int MaxJobsPerPrinterPerRun = 25;

    /// <summary>One pass over all printers. Returns how many jobs were handled.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var recovered = await claims.RecoverStuckAsync(
            timeProvider.GetUtcNow(), TimeSpan.FromMinutes(settings.StuckJobMinutes), cancellationToken);
        if (recovered > 0)
        {
            LogRecovered(recovered);
        }

        var printers = await dbContext.Printers.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync(cancellationToken);
        var handled = 0;
        foreach (var target in printers)
        {
            for (var i = 0; i < MaxJobsPerPrinterPerRun; i++)
            {
                if (!await ProcessNextAsync(target, cancellationToken))
                {
                    break;
                }

                handled++;
            }
        }

        return handled;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Returned {Count} stuck print job(s) to the queue.")]
    private partial void LogRecovered(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Print job {JobId} failed before reaching the printer.")]
    private partial void LogFailedBeforePrinter(Exception exception, long jobId);

    /// <summary>Claims and prints the next due job of one printer. Returns false when none is due.</summary>
    public async Task<bool> ProcessNextAsync(PrinterLookup target, CancellationToken cancellationToken)
    {
        var jobId = await claims.ClaimNextAsync(target.Id, timeProvider.GetUtcNow(), cancellationToken);
        if (jobId is null)
        {
            return false;
        }

        var sku = string.Empty;
        var gtin = string.Empty;
        PrintOutcome outcome;
        try
        {
            var job = await dbContext.PrintJobs.AsNoTracking().FirstAsync(j => j.Id == jobId, cancellationToken);
            var document = await tags.BuildAsync(new TagRequest(job.VariantGtinId, job.Qty, job.OutcomeId, job.PriceSnapshot), target.FormatId, cancellationToken);
            sku = document.SkuCode;
            gtin = document.Gtin;
            outcome = await printer.SendAsync(new PrinterTarget(target.Id, target.Code, target.FormatId, target.Address), document, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // shutting down: the job stays Printing and the stuck-job recovery returns it to the queue
        }
        catch (Exception ex)
        {
            LogFailedBeforePrinter(ex, jobId.Value);
            outcome = PrintOutcome.Fail(ex.Message);
        }

        await sender.Send(new CompletePrintJobCommand(jobId.Value, outcome.Success, outcome.Error, outcome.OutputReference, sku, gtin), cancellationToken);
        return true;
    }
}


/// <summary>Records the printer's answer on a claimed job (PJ-2, PJ-10). Ignores a job that is no longer Printing (it was recovered meanwhile).</summary>
public sealed record CompletePrintJobCommand(long JobId, bool Success, string? Error, string? OutputReference, string SkuCode, string Gtin)
    : IRequest, ILabellingCommand;

public sealed class CompletePrintJobCommandHandler(
    ILabellingDbContext dbContext, IOptions<LabellingOptions> options, TimeProvider timeProvider)
    : IRequestHandler<CompletePrintJobCommand>
{
    public async Task Handle(CompletePrintJobCommand request, CancellationToken cancellationToken)
    {
        var job = await dbContext.PrintJobs.FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken);
        if (job is null || job.StatusId != PrintJobStatus.Printing)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (request.Success)
        {
            job.MarkPrinted(now, request.SkuCode, request.Gtin, request.OutputReference);
        }
        else
        {
            job.RecordFailure(now, string.IsNullOrWhiteSpace(request.Error) ? "The printer reported an error." : request.Error, options.Value.Backoff, options.Value.MaxAttempts);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
