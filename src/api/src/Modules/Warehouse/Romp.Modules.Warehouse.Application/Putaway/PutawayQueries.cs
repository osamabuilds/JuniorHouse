using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Putaway;

namespace Romp.Modules.Warehouse.Application.Putaway;

/// <summary>
/// The scan lookup (PUT-6, PUT-7, PUT-17): what does this barcode belong to, is it retired, and which items are waiting to be shelved?
/// A malformed barcode is refused before any query; an unknown one answers null. Two indexed queries, nothing else, because this is
/// the hot path that must stay under 300 ms (NFR-SC-01).
/// </summary>
public sealed record ScanLookupQuery(string Gtin) : IRequest<ScanLookupDto?>;

public sealed class ScanLookupQueryValidator : AbstractValidator<ScanLookupQuery>
{
    public ScanLookupQueryValidator() =>
        RuleFor(q => q.Gtin).Must(g => Gtin13.IsValid(g)).WithMessage("That is not a valid GTIN-13 barcode.");
}

public sealed class ScanLookupQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<ScanLookupQuery, ScanLookupDto?>
{
    public async Task<ScanLookupDto?> Handle(ScanLookupQuery request, CancellationToken cancellationToken)
    {
        var variant = await dbContext.VariantRefs.AsNoTracking()
            .Where(v => v.Gtin == request.Gtin)
            .Select(v => new { v.Id, v.SkuCode, v.Gtin, v.GtinRetired, v.StyleId, v.ColourId, v.SizeId })
            .FirstOrDefaultAsync(cancellationToken);
        if (variant is null)
        {
            return null;
        }

        var items = await dbContext.PutawayItems.AsNoTracking()
            .Where(i => i.PutQuantity < i.QcQuantity
                        && (i.VariantRefId == variant.Id
                            || (i.VariantRefId == null && i.StyleId == variant.StyleId && i.ColourId == variant.ColourId && i.SizeId == variant.SizeId)))
            .OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.PoId, i.GradeId, i.QcQuantity, i.PutQuantity })
            .ToListAsync(cancellationToken);

        return new ScanLookupDto(
            variant.Id, variant.SkuCode, variant.Gtin, variant.GtinRetired,
            items.Select(i => new ScanPendingItemDto(i.Id, i.PoId, i.GradeId, Grades.CodeOf(i.GradeId), i.QcQuantity - i.PutQuantity)).ToList());
    }
}

/// <summary>
/// The putaway queue (PUT-14): items oldest first ("put away n of N"), optionally one status. Ordered by the unique item id. One COUNT,
/// one page query joined to the variant reference, so the page costs the same however many rows it holds.
/// </summary>
public sealed record SearchPutawayItemsQuery(short? StatusId = null, bool OnlyOpen = false, string? Search = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<PutawayItemRowDto>>;

public sealed class SearchPutawayItemsQueryValidator : AbstractValidator<SearchPutawayItemsQuery>
{
    public SearchPutawayItemsQueryValidator()
    {
        RuleFor(q => q.StatusId).Must(s => s is null || PutawayStatuses.IsKnown(s.Value)).WithMessage("Unknown putaway status.");
        RuleFor(q => q.Search).MaximumLength(ValidationRules.MaxSearchLength);
    }
}

public sealed class SearchPutawayItemsQueryHandler(IWarehouseDbContext dbContext)
    : IRequestHandler<SearchPutawayItemsQuery, PagedResult<PutawayItemRowDto>>
{
    public async Task<PagedResult<PutawayItemRowDto>> Handle(SearchPutawayItemsQuery request, CancellationToken cancellationToken)
    {
        var items = dbContext.PutawayItems.AsNoTracking().AsQueryable();
        if (request.StatusId is { } status)
        {
            items = items.Where(i => i.StatusId == status);
        }

        if (request.OnlyOpen)
        {
            items = items.Where(i => i.StatusId != PutawayStatuses.Complete);
        }

        // Find one item among many by SKU text or an exact barcode (the way in when there is no tag to scan, PUT-19).
        var search = request.Search?.Trim();
        var variants = dbContext.VariantRefs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(search))
        {
            var upper = search.ToUpperInvariant();
            var matching = variants.Where(v => v.SkuCode.Contains(upper) || v.Gtin == search).Select(v => v.Id);
            items = items.Where(i => i.VariantRefId != null && matching.Contains(i.VariantRefId.Value));
        }

        var page = await (
                from i in items
                join v in variants on i.VariantRefId equals v.Id into joined
                from v in joined.DefaultIfEmpty()
                orderby i.Id
                select new
                {
                    i.Id, i.SubBatchId, i.PoId, i.GradeId, i.QcQuantity, i.PutQuantity, i.StatusId, i.InsrDte,
                    Sku = v == null ? null : v.SkuCode,
                    Gtin = v == null ? null : v.Gtin,
                    Retired = v != null && v.GtinRetired,
                })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(i => new PutawayItemRowDto(
            i.Id, i.SubBatchId, i.PoId, i.Sku, i.Gtin, i.Retired, i.GradeId, Grades.CodeOf(i.GradeId), i.QcQuantity, i.PutQuantity,
            i.QcQuantity - i.PutQuantity, i.StatusId, StatusCode(i.StatusId), i.InsrDte)).ToList();
        return new PagedResult<PutawayItemRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }

    private static string StatusCode(short id) => id switch
    {
        PutawayStatuses.AwaitingGtin => "AWAITING_GTIN",
        PutawayStatuses.Pending => "PENDING",
        _ => "COMPLETE",
    };
}

/// <summary>The putaway history (PUT-14), newest first, optionally for one item.</summary>
public sealed record SearchPutawayRecordsQuery(long? ItemId = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<PutawayRecordDto>>;

public sealed class SearchPutawayRecordsQueryHandler(IWarehouseDbContext dbContext)
    : IRequestHandler<SearchPutawayRecordsQuery, PagedResult<PutawayRecordDto>>
{
    public async Task<PagedResult<PutawayRecordDto>> Handle(SearchPutawayRecordsQuery request, CancellationToken cancellationToken)
    {
        var records = dbContext.PutawayRecords.AsNoTracking().AsQueryable();
        if (request.ItemId is { } itemId)
        {
            records = records.Where(r => r.ItemId == itemId);
        }

        var page = await (
                from r in records
                join i in dbContext.PutawayItems.AsNoTracking() on r.ItemId equals i.Id
                join b in dbContext.Bins.AsNoTracking() on r.BinId equals b.Id
                join v in dbContext.VariantRefs.AsNoTracking() on i.VariantRefId equals v.Id into joined
                from v in joined.DefaultIfEmpty()
                orderby r.Id descending
                select new
                {
                    r.Id, r.ItemId, r.BinId, b.BinCode, r.Quantity, r.Scanned, r.ActorName, r.InsrDte,
                    Pending = i.QcQuantity - i.PutQuantity,
                    Sku = v == null ? null : v.SkuCode,
                })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items
            .Select(r => new PutawayRecordDto(r.Id, r.ItemId, r.BinId, r.BinCode, r.Quantity, r.Scanned, r.ActorName, r.InsrDte, r.Pending, r.Sku))
            .ToList();
        return new PagedResult<PutawayRecordDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}
