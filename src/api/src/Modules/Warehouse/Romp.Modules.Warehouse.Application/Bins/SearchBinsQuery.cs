using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;

namespace Romp.Modules.Warehouse.Application.Bins;

/// <summary>
/// The paged bin list (BIN-6), optionally for one zone. Ordered by id, which is unique. One COUNT, one page query and one stock
/// call for the whole page, however many bins exist (no N+1).
/// </summary>
public sealed record SearchBinsQuery(string? Zone = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<BinRowDto>>;

public sealed class SearchBinsQueryValidator : AbstractValidator<SearchBinsQuery>
{
    public SearchBinsQueryValidator() => RuleFor(q => q.Zone).MaximumLength(ValidationRules.MaxSearchLength);
}

public sealed class SearchBinsQueryHandler(IWarehouseDbContext dbContext, IBinStockQuery stock)
    : IRequestHandler<SearchBinsQuery, PagedResult<BinRowDto>>
{
    public async Task<PagedResult<BinRowDto>> Handle(SearchBinsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Bins.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Zone))
        {
            var zone = request.Zone.Trim().ToUpperInvariant();
            query = query.Where(b => b.Zone == zone);
        }

        var page = await query
            .OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.BinCode, b.Zone, b.Aisle, b.Shelf, b.IsActive, b.LastCountedAt })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var units = await stock.GetUnitsOnHandAsync([.. page.Items.Select(b => b.Id)], cancellationToken);
        var rows = page.Items
            .Select(b => new BinRowDto(b.Id, b.BinCode, b.Zone, b.Aisle, b.Shelf, b.IsActive, units.GetValueOrDefault(b.Id), b.LastCountedAt))
            .ToList();

        return new PagedResult<BinRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>A single bin by its printed code (the scan lookup, NFR-SC-01). Null when no such bin exists.</summary>
public sealed record GetBinByCodeQuery(string Code) : IRequest<BinRowDto?>;

public sealed class GetBinByCodeQueryValidator : AbstractValidator<GetBinByCodeQuery>
{
    public GetBinByCodeQueryValidator() => RuleFor(q => q.Code).NotEmpty().MaximumLength(12);
}

public sealed class GetBinByCodeQueryHandler(IWarehouseDbContext dbContext, IBinStockQuery stock)
    : IRequestHandler<GetBinByCodeQuery, BinRowDto?>
{
    public async Task<BinRowDto?> Handle(GetBinByCodeQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var bin = await dbContext.Bins.AsNoTracking().FirstOrDefaultAsync(b => b.BinCode == code, cancellationToken);
        if (bin is null)
        {
            return null;
        }

        var units = (await stock.GetUnitsOnHandAsync([bin.Id], cancellationToken)).GetValueOrDefault(bin.Id);
        return new BinRowDto(bin.Id, bin.BinCode, bin.Zone, bin.Aisle, bin.Shelf, bin.IsActive, units, bin.LastCountedAt);
    }
}
