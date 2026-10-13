using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.PrintJobs;

namespace Romp.Modules.Labelling.Application.PrintJobs;

/// <summary>
/// Reads print jobs joined to their variant and printer in one query (no per-row lookups). Filters and ordering are applied to
/// <see cref="PrintJobRow"/> (a member-initialised shape EF can translate) BEFORE the final projection into <see cref="PrintJobRecord"/>.
/// </summary>
internal static class PrintJobViews
{
    public static IQueryable<PrintJobRow> Rows(ILabellingDbContext dbContext) =>
        from j in dbContext.PrintJobs.AsNoTracking()
        join g in dbContext.Gtins.AsNoTracking() on j.VariantGtinId equals g.Id
        join v in dbContext.Variants.AsNoTracking() on g.VariantId equals v.Id
        join p in dbContext.Printers.AsNoTracking() on j.PrinterId equals p.Id
        select new PrintJobRow { Job = j, SkuCode = v.SkuCode, Gtin = g.Gtin, PrinterCode = p.Code };

    public static IQueryable<PrintJobRecord> Project(IQueryable<PrintJobRow> rows) =>
        rows.Select(r => new PrintJobRecord(
            r.Job.Id, r.Job.JobNo, r.Job.KindId, r.Job.StatusId, r.Job.VariantGtinId, r.Job.WorkItemId, r.SkuCode, r.Gtin, r.Job.Qty,
            r.Job.PriceSnapshot, r.PrinterCode, r.Job.AttemptCount, r.Job.LastError, r.Job.ReprintReasonId, r.Job.Note, r.Job.RequestedBy,
            r.Job.ManualActionBy, r.Job.InsrDte, r.Job.NextAttemptAt));

    public static async Task<PrintJobDto> LoadAsync(ILabellingDbContext dbContext, long id, CancellationToken cancellationToken) =>
        (await Project(Rows(dbContext).Where(r => r.Job.Id == id)).FirstAsync(cancellationToken)).ToDto();

    public static PrintJobDto ToDto(PrintJob job, string skuCode, string gtin, string printerCode) => new(
        job.Id, job.JobNo, PrintJobKind.CodeOf(job.KindId), PrintCodes.Status(job.StatusId), job.VariantGtinId, job.WorkItemId,
        skuCode, gtin, job.Qty, job.PriceSnapshot, printerCode, job.AttemptCount, job.LastError,
        job.ReprintReasonId is null ? null : PrintCodes.ReprintReason(job.ReprintReasonId), job.Note, job.RequestedBy,
        job.ManualActionBy, job.InsrDte, job.NextAttemptAt);
}

/// <summary>The paged print queue (PJ-13): newest first, optionally one status and/or one printer.</summary>
public sealed record SearchPrintJobsQuery(string? Status = null, short? PrinterId = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<PrintJobDto>>;

public sealed class SearchPrintJobsQueryHandler(ILabellingDbContext dbContext)
    : IRequestHandler<SearchPrintJobsQuery, PagedResult<PrintJobDto>>
{
    public async Task<PagedResult<PrintJobDto>> Handle(SearchPrintJobsQuery request, CancellationToken cancellationToken)
    {
        var rows = PrintJobViews.Rows(dbContext);
        if (request.PrinterId is { } printerId)
        {
            rows = rows.Where(r => r.Job.PrinterId == printerId);
        }

        if (PrintCodes.StatusId(request.Status) is { } status)
        {
            rows = rows.Where(r => r.Job.StatusId == status);
        }

        var page = await PrintJobViews.Project(rows.OrderByDescending(r => r.Job.Id)).ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<PrintJobDto>(page.Items.Select(r => r.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>Every job for a variant (any GTIN it has held), newest first: the print history on the SKU register (PJ-6).</summary>
public sealed record GetPrintHistoryQuery(long VariantId, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<PrintJobDto>>;

public sealed class GetPrintHistoryQueryHandler(ILabellingDbContext dbContext)
    : IRequestHandler<GetPrintHistoryQuery, PagedResult<PrintJobDto>>
{
    public async Task<PagedResult<PrintJobDto>> Handle(GetPrintHistoryQuery request, CancellationToken cancellationToken)
    {
        var gtinIds = dbContext.Gtins.Where(g => g.VariantId == request.VariantId).Select(g => g.Id);
        var page = await PrintJobViews.Project(PrintJobViews.Rows(dbContext)
                .Where(r => gtinIds.Contains(r.Job.VariantGtinId))
                .OrderByDescending(r => r.Job.Id))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<PrintJobDto>(page.Items.Select(r => r.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>The configured printers staff may choose from (never editable through the API, PJ-12).</summary>
public sealed record ListPrintersQuery : IRequest<IReadOnlyList<PrinterDto>>;

public sealed class ListPrintersQueryHandler(ILabellingDbContext dbContext) : IRequestHandler<ListPrintersQuery, IReadOnlyList<PrinterDto>>
{
    public async Task<IReadOnlyList<PrinterDto>> Handle(ListPrintersQuery request, CancellationToken cancellationToken) =>
        await dbContext.Printers.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortSeq).ThenBy(p => p.Id)
            .Select(p => new PrinterDto(p.Id, p.Code, p.Name, p.FormatId == PrintFormat.Zpl ? "Zpl" : "Pdf"))
            .ToListAsync(cancellationToken);
}

/// <summary>Per-work-item totals the worklist shows: tags printed so far, and tags already spoken for (queued, printing or printed).</summary>
public sealed record WorkItemPrintTotals(int PrintedQty, int ActiveQty);

/// <summary>
/// The public surface of <c>print-jobs</c> for label-work (one-way dependency): how many tags exist per work item, and when a
/// variant's tags were last put in the queue (for the "price changed since printing" flag, LW-5). Each call is one grouped query.
/// </summary>
public interface IPrintProgress
{
    Task<IReadOnlyDictionary<long, WorkItemPrintTotals>> TotalsByWorkItemAsync(
        IReadOnlyCollection<long> workItemIds, CancellationToken cancellationToken);

    /// <summary>Latest non-cancelled job time per variant (originals and reprints). Null asks for every variant that has jobs.</summary>
    Task<IReadOnlyDictionary<long, DateTimeOffset>> LatestJobAtByVariantAsync(
        IReadOnlyCollection<long>? variantIds, CancellationToken cancellationToken);
}

public sealed class PrintProgress(ILabellingDbContext dbContext) : IPrintProgress
{
    public async Task<IReadOnlyDictionary<long, WorkItemPrintTotals>> TotalsByWorkItemAsync(
        IReadOnlyCollection<long> workItemIds, CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
        {
            return new Dictionary<long, WorkItemPrintTotals>();
        }

        var ids = workItemIds.ToList();
        var rows = await dbContext.PrintJobs.AsNoTracking()
            .Where(j => j.WorkItemId != null && ids.Contains(j.WorkItemId.Value)
                && j.KindId == PrintJobKind.Original && j.StatusId != PrintJobStatus.Cancelled && j.StatusId != PrintJobStatus.Failed)
            .GroupBy(j => j.WorkItemId!.Value)
            .Select(g => new
            {
                WorkItemId = g.Key,
                Printed = g.Where(j => j.StatusId == PrintJobStatus.Printed).Sum(j => j.Qty),
                Active = g.Sum(j => j.Qty),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.WorkItemId, r => new WorkItemPrintTotals(r.Printed, r.Active));
    }

    public async Task<IReadOnlyDictionary<long, DateTimeOffset>> LatestJobAtByVariantAsync(
        IReadOnlyCollection<long>? variantIds, CancellationToken cancellationToken)
    {
        var query = from j in dbContext.PrintJobs.AsNoTracking()
                    join g in dbContext.Gtins.AsNoTracking() on j.VariantGtinId equals g.Id
                    where j.StatusId != PrintJobStatus.Cancelled
                    select new { g.VariantId, j.InsrDte };
        if (variantIds is not null)
        {
            var ids = variantIds.ToList();
            query = query.Where(x => ids.Contains(x.VariantId));
        }

        var rows = await query.GroupBy(x => x.VariantId)
            .Select(g => new { VariantId = g.Key, Latest = g.Max(x => x.InsrDte) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.VariantId, r => r.Latest);
    }
}
