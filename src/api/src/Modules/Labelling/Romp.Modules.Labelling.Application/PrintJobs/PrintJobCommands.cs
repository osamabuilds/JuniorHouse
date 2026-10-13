using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.PrintJobs;
using ValidationException = Romp.BuildingBlocks.Application.ValidationException;

namespace Romp.Modules.Labelling.Application.PrintJobs;

/// <summary>
/// The public surface of <c>print-jobs</c> for the work-item flow (PJ-1): save a job in Queued state without touching a printer.
/// The caller (label-work) has already worked out how many tags are still allowed; this command enforces the hard limits (PJ-11),
/// that the GTIN is active (PJ-8) and that the printer is a configured one (PJ-12).
/// </summary>
public sealed record QueueOriginalPrintJobCommand(
    long VariantGtinId, long WorkItemId, int Qty, short OutcomeId, decimal Price, string? PrinterCode, string ActingPerson)
    : IRequest<PrintJobDto>, ILabellingCommand;

public sealed class QueueOriginalPrintJobCommandValidator : AbstractValidator<QueueOriginalPrintJobCommand>
{
    public QueueOriginalPrintJobCommandValidator(IOptions<LabellingOptions> options)
    {
        RuleFor(c => c.Qty).InclusiveBetween(1, options.Value.MaxTagsPerJob)
            .WithMessage($"Print between 1 and {options.Value.MaxTagsPerJob:N0} tags at a time.");
        RuleFor(c => c.Price).GreaterThanOrEqualTo(0);
        RuleFor(c => c.PrinterCode).MaximumLength(30);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class QueueOriginalPrintJobCommandHandler(
    ILabellingDbContext dbContext, IVariantReader variants, IJobNumberAllocator numbers,
    IOptions<LabellingOptions> options, TimeProvider timeProvider)
    : IRequestHandler<QueueOriginalPrintJobCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(QueueOriginalPrintJobCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await variants.GetByGtinIdAsync(request.VariantGtinId, cancellationToken)
            ?? throw new DomainException("That GTIN does not exist.");
        if (snapshot.ActiveGtinId != request.VariantGtinId)
        {
            throw new DomainException($"The GTIN for {snapshot.SkuCode} has been voided, so no tags can be printed for it.");
        }

        var printer = await PrinterRules.ResolveAsync(dbContext, options.Value, request.PrinterCode, cancellationToken);
        await PrinterRules.EnsureRoomInQueueAsync(dbContext, options.Value, printer.Id, cancellationToken);

        var job = PrintJob.Original(
            await numbers.AllocateAsync(cancellationToken), request.VariantGtinId, request.WorkItemId, request.Qty,
            request.OutcomeId, request.Price, printer.Id, request.ActingPerson.Trim(), timeProvider.GetUtcNow());
        dbContext.PrintJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return PrintJobViews.ToDto(job, snapshot.SkuCode, snapshot.ActiveGtin ?? string.Empty, printer.Code);
    }
}

/// <summary>Reprints an existing SKU's tag with the same GTIN and a reason (FR-SC-07, PJ-5..PJ-8). A retired GTIN is refused.</summary>
public sealed record ReprintTagCommand(long VariantId, int Qty, short ReasonId, string? Note, string? PrinterCode, string ActingPerson)
    : IRequest<PrintJobDto>, ILabellingCommand;

public sealed class ReprintTagCommandValidator : AbstractValidator<ReprintTagCommand>
{
    public ReprintTagCommandValidator(IOptions<LabellingOptions> options)
    {
        RuleFor(c => c.Qty).InclusiveBetween(1, options.Value.MaxTagsPerJob)
            .WithMessage($"Print between 1 and {options.Value.MaxTagsPerJob:N0} tags at a time.");
        RuleFor(c => c.ReasonId).Must(ReprintReasons.IsKnown).WithMessage("Choose why the tag is being reprinted.");
        RuleFor(c => c.Note).OptionalNote();
        RuleFor(c => c.PrinterCode).MaximumLength(30);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class ReprintTagCommandHandler(
    ILabellingDbContext dbContext, IVariantReader variants, ILabelStyleLookup lookup, IJobNumberAllocator numbers,
    IOptions<LabellingOptions> options, TimeProvider timeProvider)
    : IRequestHandler<ReprintTagCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(ReprintTagCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await variants.GetByVariantIdAsync(request.VariantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Variant {request.VariantId} was not found.");
        if (snapshot.ActiveGtinId is not { } gtinId || snapshot.ActiveGtin is null)
        {
            throw new DomainException($"The GTIN for {snapshot.SkuCode} has been voided; tags for it can no longer be printed.");
        }

        // D4: a reprint prints the CURRENT price, which is how a price change is put right.
        var style = (await lookup.GetStylesAsync([snapshot.StyleId], cancellationToken)).GetValueOrDefault(snapshot.StyleId)
            ?? throw new DomainException("The style for this SKU could not be found, so the price is unknown.");

        // The tag keeps the "2ND" mark if the stock was concession stock: take it from the latest earlier job for this GTIN.
        var outcome = await dbContext.PrintJobs.AsNoTracking()
            .Where(j => j.VariantGtinId == gtinId)
            .OrderByDescending(j => j.Id)
            .Select(j => (short?)j.OutcomeId)
            .FirstOrDefaultAsync(cancellationToken) ?? LabelOutcome.Accepted;

        var printer = await PrinterRules.ResolveAsync(dbContext, options.Value, request.PrinterCode, cancellationToken);
        await PrinterRules.EnsureRoomInQueueAsync(dbContext, options.Value, printer.Id, cancellationToken);

        var job = PrintJob.Reprint(
            await numbers.AllocateAsync(cancellationToken), gtinId, request.Qty, outcome, style.TargetRetailPrice, printer.Id,
            request.ReasonId, request.Note?.Trim(), request.ActingPerson.Trim(), timeProvider.GetUtcNow());
        dbContext.PrintJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return PrintJobViews.ToDto(job, snapshot.SkuCode, snapshot.ActiveGtin, printer.Code);
    }
}

/// <summary>Puts a Failed job back in the queue (PJ-3): the same job, attempts reset, no new job or GTIN.</summary>
public sealed record RetryPrintJobCommand(long JobId, string ActingPerson) : IRequest<PrintJobDto>, ILabellingCommand;

public sealed class RetryPrintJobCommandValidator : AbstractValidator<RetryPrintJobCommand>
{
    public RetryPrintJobCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class RetryPrintJobCommandHandler(ILabellingDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<RetryPrintJobCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(RetryPrintJobCommand request, CancellationToken cancellationToken)
    {
        var job = await dbContext.PrintJobs.FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Print job {request.JobId} was not found.");
        job.Retry(timeProvider.GetUtcNow(), request.ActingPerson.Trim());
        await dbContext.SaveChangesAsync(cancellationToken);
        return await PrintJobViews.LoadAsync(dbContext, job.Id, cancellationToken);
    }
}

/// <summary>Cancels a Queued or Failed job. A job that is printing right now cannot be cancelled.</summary>
public sealed record CancelPrintJobCommand(long JobId, string ActingPerson) : IRequest<PrintJobDto>, ILabellingCommand;

public sealed class CancelPrintJobCommandValidator : AbstractValidator<CancelPrintJobCommand>
{
    public CancelPrintJobCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class CancelPrintJobCommandHandler(ILabellingDbContext dbContext)
    : IRequestHandler<CancelPrintJobCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(CancelPrintJobCommand request, CancellationToken cancellationToken)
    {
        var job = await dbContext.PrintJobs.FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Print job {request.JobId} was not found.");
        job.Cancel(request.ActingPerson.Trim());
        await dbContext.SaveChangesAsync(cancellationToken);
        return await PrintJobViews.LoadAsync(dbContext, job.Id, cancellationToken);
    }
}

/// <summary>Rules about printers shared by the commands: only a configured, active printer, and a bounded queue (PJ-11, PJ-12).</summary>
internal static class PrinterRules
{
    public static async Task<PrinterLookup> ResolveAsync(
        ILabellingDbContext dbContext, LabellingOptions options, string? requestedCode, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(requestedCode) ? options.DefaultPrinterCode : requestedCode.Trim();
        return await dbContext.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code && p.IsActive, cancellationToken)
            ?? throw new ValidationException(new Dictionary<string, string[]> { ["PrinterCode"] = [$"There is no active printer '{code}'."] });
    }

    public static async Task EnsureRoomInQueueAsync(
        ILabellingDbContext dbContext, LabellingOptions options, short printerId, CancellationToken cancellationToken)
    {
        var queued = await dbContext.PrintJobs.CountAsync(
            j => j.PrinterId == printerId && j.StatusId == PrintJobStatus.Queued, cancellationToken);
        if (queued >= options.MaxQueuedJobsPerPrinter)
        {
            throw new DomainException(
                $"That printer already has {queued} jobs waiting (the limit is {options.MaxQueuedJobsPerPrinter}). Let it catch up or cancel some jobs.");
        }
    }
}
