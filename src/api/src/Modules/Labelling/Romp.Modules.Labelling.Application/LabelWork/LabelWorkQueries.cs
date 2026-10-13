using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;

namespace Romp.Modules.Labelling.Application.LabelWork;

public sealed record LabelWorkItemDto(
    long Id,
    long SubBatchId,
    long QcBatchId,
    long PoId,
    long? StyleId,
    string? StyleCode,
    string? StyleName,
    bool StyleIsActive,
    short SizeId,
    string SizeName,
    short ColourId,
    string ColourName,
    int Qty,
    string OutcomeCode,
    string StatusCode,
    string? ParkReasonCode,
    long? VariantId,
    string? SkuCode,
    string? Gtin,
    int PrintedQty,
    int ActiveQty,
    int RemainingQty,
    bool PriceChanged,
    DateTimeOffset CreatedAt);

/// <summary>Builds worklist rows for a set of items with one lookup call per kind (LW-7: no N+1).</summary>
internal sealed class LabelWorkViews(
    ILabellingDbContext dbContext, ILabelStyleLookup lookup, IPrintProgress progress, IOptions<LabellingOptions> options)
{
    public async Task<IReadOnlyList<LabelWorkItemDto>> ToDtosAsync(IReadOnlyList<LabelWorkItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var styleIds = items.Select(i => i.StyleId).OfType<long>().Distinct().ToList();
        var styles = await lookup.GetStylesAsync(styleIds, cancellationToken);
        var colours = await lookup.GetColoursAsync(items.Select(i => i.ColourId).Distinct().ToList(), cancellationToken);
        var sizes = await lookup.GetSizesAsync(items.Select(i => i.SizeId).Distinct().ToList(), cancellationToken);
        var totals = await progress.TotalsByWorkItemAsync(items.Select(i => i.Id).ToList(), cancellationToken);

        var variantIds = items.Select(i => i.VariantId).OfType<long>().Distinct().ToList();
        var latestJobs = await progress.LatestJobAtByVariantAsync(variantIds, cancellationToken);
        var identities = variantIds.Count == 0
            ? new Dictionary<long, (string Sku, string? Gtin)>()
            : (await dbContext.Variants.AsNoTracking().Where(v => variantIds.Contains(v.Id))
                .Select(v => new
                {
                    v.Id,
                    v.SkuCode,
                    Gtin = v.Gtins.Where(g => g.StatusId == GtinStatus.Active).Select(g => g.Gtin).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken)).ToDictionary(v => v.Id, v => (v.SkuCode, v.Gtin));

        return items.Select(item =>
        {
            LabelStyle? style = null;
            if (item.StyleId is { } sid)
            {
                styles.TryGetValue(sid, out style);
            }

            totals.TryGetValue(item.Id, out var t);
            var active = t?.ActiveQty ?? 0;
            (string Sku, string? Gtin) identity = item.VariantId is { } vid && identities.TryGetValue(vid, out var found) ? found : (string.Empty, null);

            return new LabelWorkItemDto(
                item.Id, item.SubBatchId, item.QcBatchId, item.PoId, item.StyleId, style?.Code, style?.Name, style?.IsActive ?? true,
                item.SizeId, sizes.TryGetValue(item.SizeId, out var size) ? size.Name : "?",
                item.ColourId, colours.TryGetValue(item.ColourId, out var colour) ? colour.Name : "?",
                item.Qty, LabelOutcome.CodeOf(item.OutcomeId),
                item.StatusId == LabelWorkStatus.Ready ? "Ready" : "NeedsAttention",
                item.ParkReasonId switch
                {
                    ParkReasons.NoStyle => "NoStyle",
                    ParkReasons.StyleRetired => "StyleRetired",
                    ParkReasons.UnknownCode => "UnknownCode",
                    _ => null,
                },
                item.VariantId, identity.Sku.Length == 0 ? null : identity.Sku, identity.Gtin,
                t?.PrintedQty ?? 0, active,
                Math.Max(0, LabelWorkQuantities.MaxPrintable(item.Qty, options.Value.SparePercent) - active),
                PriceChangedSincePrinting(item, style, latestJobs),
                item.InsrDte);
        }).ToList();
    }

    /// <summary>LW-5: tags were put in the queue for this variant, and the style's price changed after the latest of them.</summary>
    internal static bool PriceChangedSincePrinting(LabelWorkItem item, LabelStyle? style, IReadOnlyDictionary<long, DateTimeOffset> latestJobs) =>
        item.VariantId is { } variantId
        && style is not null
        && latestJobs.TryGetValue(variantId, out var latest)
        && style.TargetPriceChangedAt > latest;
}

/// <summary>
/// The paged worklist (LW-7). <c>Status</c> is <c>ready</c> or <c>attention</c>; <c>PriceFlag</c> true keeps only items whose price has
/// changed since their tags were queued (LW-5). Ordered by id descending, which is unique.
/// </summary>
public sealed record SearchLabelWorkQuery(
    string? Status = null, long? StyleId = null, bool? PriceFlag = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<LabelWorkItemDto>>;

public sealed class SearchLabelWorkQueryHandler(
    ILabellingDbContext dbContext, ILabelStyleLookup lookup, IPrintProgress progress, IOptions<LabellingOptions> options)
    : IRequestHandler<SearchLabelWorkQuery, PagedResult<LabelWorkItemDto>>
{
    public async Task<PagedResult<LabelWorkItemDto>> Handle(SearchLabelWorkQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkItems.AsNoTracking().AsQueryable();

        switch (request.Status?.Trim().ToLowerInvariant())
        {
            case "ready":
                query = query.Where(i => i.StatusId == LabelWorkStatus.Ready);
                break;
            case "attention":
                query = query.Where(i => i.StatusId == LabelWorkStatus.NeedsAttention);
                break;
        }

        if (request.StyleId is { } styleId)
        {
            query = query.Where(i => i.StyleId == styleId);
        }

        if (request.PriceFlag == true)
        {
            var flagged = await FlaggedItemIdsAsync(cancellationToken);
            query = query.Where(i => flagged.Contains(i.Id));
        }

        var page = await query.OrderByDescending(i => i.Id)
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var views = new LabelWorkViews(dbContext, lookup, progress, options);
        return new PagedResult<LabelWorkItemDto>(await views.ToDtosAsync(page.Items, cancellationToken), page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>The price flag depends on another module's data, so it is evaluated over the (small) set of items that have jobs.</summary>
    private async Task<List<long>> FlaggedItemIdsAsync(CancellationToken cancellationToken)
    {
        var latestJobs = await progress.LatestJobAtByVariantAsync(null, cancellationToken);
        if (latestJobs.Count == 0)
        {
            return [];
        }

        var variantIds = latestJobs.Keys.ToList();
        var candidates = await dbContext.WorkItems.AsNoTracking()
            .Where(i => i.VariantId != null && variantIds.Contains(i.VariantId.Value))
            .ToListAsync(cancellationToken);
        var styles = await lookup.GetStylesAsync(candidates.Select(i => i.StyleId).OfType<long>().Distinct().ToList(), cancellationToken);

        return candidates
            .Where(i => i.StyleId is { } sid && LabelWorkViews.PriceChangedSincePrinting(i, styles.GetValueOrDefault(sid), latestJobs))
            .Select(i => i.Id)
            .ToList();
    }
}

public sealed record GetLabelWorkItemQuery(long Id) : IRequest<LabelWorkItemDto>;

public sealed class GetLabelWorkItemQueryHandler(
    ILabellingDbContext dbContext, ILabelStyleLookup lookup, IPrintProgress progress, IOptions<LabellingOptions> options)
    : IRequestHandler<GetLabelWorkItemQuery, LabelWorkItemDto>
{
    public async Task<LabelWorkItemDto> Handle(GetLabelWorkItemQuery request, CancellationToken cancellationToken)
    {
        var item = await dbContext.WorkItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Work item {request.Id} was not found.");
        return (await new LabelWorkViews(dbContext, lookup, progress, options).ToDtosAsync([item], cancellationToken))[0];
    }
}

/// <summary>A one-tag PDF of what will be printed for a work item (TAG-8): the same tag data the printer gets.</summary>
public sealed record GetTagPreviewQuery(long WorkItemId) : IRequest<TagPreviewDto>;

public sealed record TagPreviewDto(byte[] Content, string FileName);

public sealed class GetTagPreviewQueryHandler(
    ILabellingDbContext dbContext, IVariantReader variants, ILabelStyleLookup lookup, ITagDocumentBuilder tags)
    : IRequestHandler<GetTagPreviewQuery, TagPreviewDto>
{
    public async Task<TagPreviewDto> Handle(GetTagPreviewQuery request, CancellationToken cancellationToken)
    {
        var item = await dbContext.WorkItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.WorkItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work item {request.WorkItemId} was not found.");
        if (item.VariantId is not { } variantId || item.StyleId is not { } styleId)
        {
            throw new DomainException("There is nothing to preview yet: this item has no SKU.");
        }

        var snapshot = await variants.GetByVariantIdAsync(variantId, cancellationToken)
            ?? throw new DomainException("The variant for this item no longer exists.");
        if (snapshot.ActiveGtinId is not { } gtinId)
        {
            throw new DomainException($"The GTIN for {snapshot.SkuCode} has been voided, so there is no tag to preview.");
        }

        var style = (await lookup.GetStylesAsync([styleId], cancellationToken)).GetValueOrDefault(styleId)
            ?? throw new DomainException("The style for this item could not be found, so the price is unknown.");

        var document = await tags.BuildAsync(new TagRequest(gtinId, 1, item.OutcomeId, style.TargetRetailPrice), PrintFormat.Pdf, cancellationToken);
        return new TagPreviewDto(document.Content, $"tag-{snapshot.SkuCode}.pdf");
    }
}
