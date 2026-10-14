using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain;

namespace Romp.Modules.Warehouse.Application.StockLedger;

/// <summary>
/// Stock per variant (LED-5, LED-6, LED-8): on hand by grade, and the bins each variant sits in so pick staff know where it is.
/// <c>Search</c> matches the SKU text or an exact GTIN. Ordered by the unique variant id. One COUNT, one page query, one bins query
/// for the whole page and one variant query, however many variants exist (no N+1).
/// </summary>
public sealed record SearchStockQuery(string? Search = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<StockVariantRowDto>>;

public sealed class SearchStockQueryValidator : AbstractValidator<SearchStockQuery>
{
    public SearchStockQueryValidator() => RuleFor(q => q.Search).MaximumLength(ValidationRules.MaxSearchLength);
}

public sealed class SearchStockQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<SearchStockQuery, PagedResult<StockVariantRowDto>>
{
    public async Task<PagedResult<StockVariantRowDto>> Handle(SearchStockQuery request, CancellationToken cancellationToken)
    {
        var balances = dbContext.Balances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim();
            var upper = text.ToUpperInvariant();
            balances = balances.Where(b => dbContext.VariantRefs.Any(v => v.Id == b.VariantRefId && (v.SkuCode.Contains(upper) || v.Gtin == text)));
        }

        var page = await balances
            .GroupBy(b => b.VariantRefId)
            .Select(g => new
            {
                VariantRefId = g.Key,
                Accepted = g.Where(x => x.GradeId == Grades.Accepted).Sum(x => x.Quantity),
                Concession = g.Where(x => x.GradeId == Grades.Concession).Sum(x => x.Quantity),
            })
            .OrderBy(x => x.VariantRefId)
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var ids = page.Items.Select(i => i.VariantRefId).ToList();
        var variants = await dbContext.VariantRefs.AsNoTracking().Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
        var bins = await (
                from b in dbContext.Balances.AsNoTracking()
                join bin in dbContext.Bins.AsNoTracking() on b.BinId equals bin.Id
                where ids.Contains(b.VariantRefId) && b.Quantity > 0
                orderby bin.BinCode, b.GradeId
                select new { b.VariantRefId, b.BinId, bin.BinCode, b.GradeId, b.Quantity })
            .ToListAsync(cancellationToken);

        var rows = page.Items.Select(i =>
        {
            var v = variants[i.VariantRefId];
            var onHand = i.Accepted + i.Concession;
            return new StockVariantRowDto(
                v.Id, v.VariantId, v.SkuCode, v.Gtin, v.GtinRetired, onHand, onHand, i.Accepted, i.Concession,
                bins.Where(b => b.VariantRefId == i.VariantRefId)
                    .Select(b => new StockBinQuantityDto(b.BinId, b.BinCode, b.GradeId, Grades.CodeOf(b.GradeId), b.Quantity))
                    .ToList());
        }).ToList();

        return new PagedResult<StockVariantRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>What is in one bin (LED-7), one row per variant and grade, paged and ordered by SKU then grade.</summary>
public sealed record GetBinContentsQuery(long BinId, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<BinContentDto>>;

public sealed class GetBinContentsQueryValidator : AbstractValidator<GetBinContentsQuery>
{
    public GetBinContentsQueryValidator() => RuleFor(q => q.BinId).GreaterThan(0);
}

public sealed class GetBinContentsQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<GetBinContentsQuery, PagedResult<BinContentDto>>
{
    public async Task<PagedResult<BinContentDto>> Handle(GetBinContentsQuery request, CancellationToken cancellationToken)
    {
        var page = await (
                from b in dbContext.Balances.AsNoTracking()
                join v in dbContext.VariantRefs.AsNoTracking() on b.VariantRefId equals v.Id
                where b.BinId == request.BinId && b.Quantity > 0
                orderby v.SkuCode, b.GradeId
                select new { v.Id, v.SkuCode, v.Gtin, b.GradeId, b.Quantity })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(i => new BinContentDto(i.Id, i.SkuCode, i.Gtin, i.GradeId, Grades.CodeOf(i.GradeId), i.Quantity)).ToList();
        return new PagedResult<BinContentDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>The ledger history (LED-9), newest first, optionally for one variant, bin or kind.</summary>
public sealed record GetMovementsQuery(long? VariantRefId = null, long? BinId = null, short? KindId = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<StockMovementDto>>;

public sealed class GetMovementsQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<GetMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<PagedResult<StockMovementDto>> Handle(GetMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Movements.AsNoTracking().AsQueryable();
        if (request.VariantRefId is { } variant)
        {
            query = query.Where(m => m.VariantRefId == variant);
        }

        if (request.BinId is { } bin)
        {
            query = query.Where(m => m.BinId == bin);
        }

        if (request.KindId is { } kind)
        {
            query = query.Where(m => m.KindId == kind);
        }

        var page = await (
                from m in query
                join v in dbContext.VariantRefs.AsNoTracking() on m.VariantRefId equals v.Id
                join b in dbContext.Bins.AsNoTracking() on m.BinId equals b.Id
                orderby m.Id descending
                select new
                {
                    m.Id, m.VariantRefId, v.SkuCode, m.BinId, b.BinCode, m.GradeId, m.KindId, m.SourceId,
                    m.QuantityChange, m.BalanceBefore, m.BalanceAfter, m.ActorName, m.InsrDte,
                })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(m => new StockMovementDto(
            m.Id, m.VariantRefId, m.SkuCode, m.BinId, m.BinCode, Grades.CodeOf(m.GradeId), MovementKinds.CodeOf(m.KindId), m.SourceId,
            m.QuantityChange, m.BalanceBefore, m.BalanceAfter, m.ActorName, m.InsrDte)).ToList();
        return new PagedResult<StockMovementDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>
/// Compares the sum of the ledger with the stored balances for every variant, grade and bin (LED-2). Returns the mismatches: an empty
/// list means the books agree. An admin check, not a hot path; it reads whole tables, so it is not paged.
/// </summary>
public sealed record StockConsistencyQuery : IRequest<IReadOnlyList<ConsistencyMismatchDto>>;

public sealed class StockConsistencyQueryHandler(IWarehouseDbContext dbContext)
    : IRequestHandler<StockConsistencyQuery, IReadOnlyList<ConsistencyMismatchDto>>
{
    public async Task<IReadOnlyList<ConsistencyMismatchDto>> Handle(StockConsistencyQuery request, CancellationToken cancellationToken)
    {
        var ledger = await dbContext.Movements.AsNoTracking()
            .GroupBy(m => new { m.VariantRefId, m.GradeId, m.BinId })
            .Select(g => new { g.Key.VariantRefId, g.Key.GradeId, g.Key.BinId, Total = g.Sum(m => m.QuantityChange) })
            .ToListAsync(cancellationToken);
        var balances = await dbContext.Balances.AsNoTracking()
            .Select(b => new { b.VariantRefId, b.GradeId, b.BinId, b.Quantity })
            .ToListAsync(cancellationToken);

        var keys = ledger.Select(l => (l.VariantRefId, l.GradeId, l.BinId)).Union(balances.Select(b => (b.VariantRefId, b.GradeId, b.BinId)));
        var ledgerByKey = ledger.ToDictionary(l => (l.VariantRefId, l.GradeId, l.BinId), l => l.Total);
        var balanceByKey = balances.ToDictionary(b => (b.VariantRefId, b.GradeId, b.BinId), b => b.Quantity);

        return keys
            .Select(k => new ConsistencyMismatchDto(k.Item1, k.Item2, k.Item3, ledgerByKey.GetValueOrDefault(k), balanceByKey.GetValueOrDefault(k)))
            .Where(m => m.Ledger != m.Balance)
            .ToList();
    }
}
