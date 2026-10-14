using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;
using ValidationException = Romp.BuildingBlocks.Application.ValidationException;

namespace Romp.Modules.Labelling.Application.LabelWork;

/// <summary>
/// Stores the accepted lines of one QC sub-batch as work items and prepares them (LW-1, LW-2, LW-4). Idempotent: lines that already
/// exist for the sub-batch are skipped, so a redelivered message changes nothing and takes no GTIN number.
/// </summary>
public sealed record RecordAcceptedBatchCommand(MappedBatch Batch) : IRequest, ILabellingCommand;

public sealed class RecordAcceptedBatchCommandHandler(ILabellingDbContext dbContext, LabelWorkResolver resolver)
    : IRequestHandler<RecordAcceptedBatchCommand>
{
    public async Task Handle(RecordAcceptedBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = request.Batch;
        var existing = await dbContext.WorkItems.AsNoTracking()
            .Where(i => i.SubBatchId == batch.SubBatchId)
            .Select(i => new { i.SizeId, i.ColourId })
            .ToListAsync(cancellationToken);
        var known = existing.Select(e => (e.SizeId, e.ColourId)).ToHashSet();

        var items = batch.Lines
            .Where(l => !known.Contains((l.SizeId, l.ColourId)))
            .Select(l => new LabelWorkItem(batch.SubBatchId, batch.QcBatchId, batch.PoId, l.SizeId, l.ColourId, l.Qty, batch.OutcomeId))
            .ToList();
        if (items.Count == 0)
        {
            return;
        }

        dbContext.WorkItems.AddRange(items);
        await dbContext.SaveChangesAsync(cancellationToken);
        await resolver.ResolveAsync(items, confirmRetiredStyle: false, cancellationToken);
    }
}

/// <summary>Re-runs a parked item once its cause is fixed (LW-4). For a retired style, <c>ConfirmRetiredStyle</c> lets the goods that exist be labelled (Q15).</summary>
public sealed record ResolveWorkItemCommand(long WorkItemId, bool ConfirmRetiredStyle, string ActingPerson)
    : IRequest<LabelWorkItemDto>, ILabellingCommand;

public sealed class ResolveWorkItemCommandValidator : AbstractValidator<ResolveWorkItemCommand>
{
    public ResolveWorkItemCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class ResolveWorkItemCommandHandler(ILabellingDbContext dbContext, IRowLocks locks, LabelWorkResolver resolver, ISender sender)
    : IRequestHandler<ResolveWorkItemCommand, LabelWorkItemDto>
{
    public async Task<LabelWorkItemDto> Handle(ResolveWorkItemCommand request, CancellationToken cancellationToken)
    {
        await locks.LockWorkItemAsync(request.WorkItemId, cancellationToken);
        var item = await dbContext.WorkItems.FirstOrDefaultAsync(i => i.Id == request.WorkItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work item {request.WorkItemId} was not found.");

        if (item.StatusId != LabelWorkStatus.Ready)
        {
            await resolver.ResolveAsync([item], request.ConfirmRetiredStyle, cancellationToken);
        }

        return await sender.Send(new GetLabelWorkItemQuery(item.Id), cancellationToken);
    }
}

/// <summary>
/// Queues tags for a work item (PJ-1). This feature works out how many tags are still allowed (accepted quantity plus the
/// configured spare allowance, minus tags already queued, D10) under a row lock so two clicks cannot both pass, then hands the job
/// to <c>print-jobs</c>, which owns the queue.
/// </summary>
public sealed record QueueTagsCommand(long WorkItemId, int Qty, string? PrinterCode, string ActingPerson)
    : IRequest<PrintJobDto>, ILabellingCommand;

public sealed class QueueTagsCommandValidator : AbstractValidator<QueueTagsCommand>
{
    public QueueTagsCommandValidator(IOptions<LabellingOptions> options)
    {
        RuleFor(c => c.Qty).InclusiveBetween(1, options.Value.MaxTagsPerJob)
            .WithMessage($"Print between 1 and {options.Value.MaxTagsPerJob:N0} tags at a time.");
        RuleFor(c => c.PrinterCode).MaximumLength(30);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class QueueTagsCommandHandler(
    ILabellingDbContext dbContext, IRowLocks locks, Variants.IVariantReader variants, ILabelStyleLookup lookup,
    IPrintProgress progress, IOptions<LabellingOptions> options, ISender sender)
    : IRequestHandler<QueueTagsCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(QueueTagsCommand request, CancellationToken cancellationToken)
    {
        await locks.LockWorkItemAsync(request.WorkItemId, cancellationToken);

        var item = await dbContext.WorkItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.WorkItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work item {request.WorkItemId} was not found.");
        if (item.StatusId != LabelWorkStatus.Ready || item.VariantId is not { } variantId || item.StyleId is not { } styleId)
        {
            throw new DomainException("This item cannot be printed yet: resolve what it is waiting for first.");
        }

        var snapshot = await variants.GetByVariantIdAsync(variantId, cancellationToken)
            ?? throw new DomainException("The variant for this item no longer exists.");
        if (snapshot.ActiveGtinId is not { } gtinId)
        {
            throw new DomainException($"The GTIN for {snapshot.SkuCode} has been voided. Resolve the item again to give the variant a new GTIN.");
        }

        var style = (await lookup.GetStylesAsync([styleId], cancellationToken)).GetValueOrDefault(styleId)
            ?? throw new DomainException("The style for this item could not be found, so the price is unknown.");

        var totals = await progress.TotalsByWorkItemAsync([item.Id], cancellationToken);
        var active = totals.TryGetValue(item.Id, out var t) ? t.ActiveQty : 0;
        var allowed = LabelWorkQuantities.MaxPrintable(item.Qty, options.Value.SparePercent) - active;
        if (request.Qty > allowed)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Qty)] = [allowed <= 0
                    ? "All the tags for this item are already queued or printed. Use a reprint if a tag was lost."
                    : $"Only {allowed:N0} more tags are allowed for this item."],
            });
        }

        return await sender.Send(
            new QueueOriginalPrintJobCommand(gtinId, item.Id, request.Qty, item.OutcomeId, style.TargetRetailPrice, request.PrinterCode, request.ActingPerson),
            cancellationToken);
    }
}

/// <summary>The quantity arithmetic of D10, in one place so the screen, the check and the tests agree.</summary>
public static class LabelWorkQuantities
{
    /// <summary>Accepted quantity plus the spare allowance (percent, rounded up). 0% by default.</summary>
    public static int MaxPrintable(int acceptedQty, decimal sparePercent) =>
        acceptedQty + (int)Math.Ceiling(acceptedQty * sparePercent / 100m);
}
