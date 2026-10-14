using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Application.Variants;

/// <summary>
/// The paged SKU register (VAR-13). <c>Search</c> matches the SKU text or an exact GTIN (any GTIN the variant has held);
/// <c>Status</c> is <c>active</c> (has an active GTIN), <c>retired</c> (none), or empty for all. Ordered by id descending, which is unique.
/// One COUNT, one page query, and one lookup call per kind, however many variants exist.
/// </summary>
public sealed record SearchVariantsQuery(string? Search = null, string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<VariantRowDto>>;

public sealed class SearchVariantsQueryValidator : AbstractValidator<SearchVariantsQuery>
{
    public SearchVariantsQueryValidator() =>
        RuleFor(q => q.Search).MaximumLength(ValidationRules.MaxSearchLength);
}

public sealed class SearchVariantsQueryHandler(ILabellingDbContext dbContext, ILabelStyleLookup lookup)
    : IRequestHandler<SearchVariantsQuery, PagedResult<VariantRowDto>>
{
    public async Task<PagedResult<VariantRowDto>> Handle(SearchVariantsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Variants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim();
            var upper = text.ToUpperInvariant();
            query = query.Where(v => v.SkuCode.Contains(upper) || v.Gtins.Any(g => g.Gtin == text));
        }

        switch (request.Status?.Trim().ToLowerInvariant())
        {
            case "active":
                query = query.Where(v => v.Gtins.Any(g => g.StatusId == GtinStatus.Active));
                break;
            case "retired":
                query = query.Where(v => !v.Gtins.Any(g => g.StatusId == GtinStatus.Active));
                break;
        }

        var page = await query
            .OrderByDescending(v => v.Id)
            .Select(v => new VariantRecord(
                v.Id, v.StyleId, v.ColourId, v.SizeId, v.SkuCode,
                v.Gtins.Where(g => g.StatusId == GtinStatus.Active).Select(g => g.Gtin).FirstOrDefault(),
                v.Gtins.Count(g => g.StatusId == GtinStatus.Retired),
                v.InsrDte))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = await VariantViews.ToRowsAsync(page.Items, lookup, cancellationToken);
        return new PagedResult<VariantRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One variant with every GTIN it has held (VAR-8: the history of voids stays visible).</summary>
public sealed record GetVariantQuery(long Id) : IRequest<VariantDetailDto>;

public sealed class GetVariantQueryHandler(ILabellingDbContext dbContext, ILabelStyleLookup lookup)
    : IRequestHandler<GetVariantQuery, VariantDetailDto>
{
    public async Task<VariantDetailDto> Handle(GetVariantQuery request, CancellationToken cancellationToken)
    {
        var variant = await dbContext.Variants.AsNoTracking().Include(v => v.Gtins)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Variant {request.Id} was not found.");
        return await BuildDetailAsync(variant, lookup, cancellationToken);
    }

    internal static async Task<VariantDetailDto> BuildDetailAsync(Variant variant, ILabelStyleLookup lookup, CancellationToken cancellationToken)
    {
        var record = new VariantRecord(
            variant.Id, variant.StyleId, variant.ColourId, variant.SizeId, variant.SkuCode,
            variant.ActiveGtin?.Gtin, variant.Gtins.Count(g => g.StatusId == GtinStatus.Retired), variant.InsrDte);
        var row = (await VariantViews.ToRowsAsync([record], lookup, cancellationToken))[0];
        return new VariantDetailDto(row, variant.Gtins.OrderByDescending(g => g.Id).Select(VariantViews.ToDto).ToList());
    }
}

/// <summary>How much of the GTIN range is used (VAR-12). Warns at 80% so GS1 can be asked for more in good time.</summary>
public sealed record GetGtinCapacityQuery : IRequest<GtinCapacityDto>;

public sealed class GetGtinCapacityQueryHandler(IGtinSequence sequence, IOptions<LabellingOptions> options)
    : IRequestHandler<GetGtinCapacityQuery, GtinCapacityDto>
{
    public const double WarningFraction = 0.8;

    public async Task<GtinCapacityDto> Handle(GetGtinCapacityQuery request, CancellationToken cancellationToken)
    {
        var prefix = options.Value.Prefix;
        var used = await sequence.CurrentAsync(prefix.Value, cancellationToken);
        var fraction = (double)used / prefix.Capacity;
        return new GtinCapacityDto(
            prefix.Value, prefix.IsTestPrefix, prefix.Capacity, used, Math.Max(0, prefix.Capacity - 1 - used),
            Math.Round(fraction * 100, 1), fraction >= WarningFraction);
    }
}
