using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Returns.Application.Abstractions;
using Romp.Modules.Returns.Domain;
using Romp.Modules.Returns.Domain.ReverseQc;

namespace Romp.Modules.Returns.Application.ReverseQc;

public sealed record ReturnCaseSummaryDto(
    long Id, long OrderId, string OrderNumber, string Status, int LineCount, int DecidedCount, DateTimeOffset OpenedAt, DateTimeOffset? CompletedAt);

public sealed record ReturnLineDto(
    long Id, long VariantId, string? Sku, string Name, string Colour, string Size, int OrderedQuantity, string? Decision, int? DecidedQuantity, string? Note, string? DecidedBy, DateTimeOffset? DecidedAt);

public sealed record ReturnCaseDetailDto(
    long Id, long OrderId, string OrderNumber, string Status, bool? SealIntact, string? ReceivingBinCode, string? ReceivedBy, DateTimeOffset? ReceivedAt,
    string? CompletedBy, DateTimeOffset? CompletedAt, DateTimeOffset OpenedAt, IReadOnlyList<ReturnLineDto> Lines);

internal static class ReturnCaseMapper
{
    public static ReturnCaseDetailDto ToDetail(ReturnCase c) => new(
        c.Id, c.OrderId, c.OrderNumber, ReturnStatuses.CodeOf(c.StatusId), c.SealIntact, c.ReceivingBinCode, c.ReceivedBy, c.ReceivedAt, c.CompletedBy, c.CompletedAt, c.InsrDte,
        [.. c.Lines.OrderBy(l => l.Id).Select(l => new ReturnLineDto(
            l.Id, l.VariantId, l.Sku, l.Name, l.Colour, l.Size, l.OrderedQuantity, l.DecisionId is { } d ? ReturnDecisions.CodeOf(d) : null, l.DecidedQuantity, l.Note, l.DecidedBy, l.DecidedAt))]);
}

/// <summary>The cases, newest first, optionally one status (RQC-11). One page, counts inside the same query: no per-row queries.</summary>
public sealed record ListReturnCasesQuery(string? Status, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<ReturnCaseSummaryDto>>;

public sealed class ListReturnCasesQueryHandler(IReturnsDbContext dbContext) : IRequestHandler<ListReturnCasesQuery, PagedResult<ReturnCaseSummaryDto>>
{
    public async Task<PagedResult<ReturnCaseSummaryDto>> Handle(ListReturnCasesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Cases.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ReturnStatuses.FromCode(request.Status) ?? throw new DomainException("Unknown status.");
            query = query.Where(c => c.StatusId == status);
        }

        var page = await query.OrderByDescending(c => c.InsrDte).ThenByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id, c.OrderId, c.OrderNumber, c.StatusId, c.InsrDte, c.CompletedAt,
                Lines = c.Lines.Count,
                Decided = c.Lines.Count(l => l.DecisionId != null),
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<ReturnCaseSummaryDto>(
            [.. page.Items.Select(c => new ReturnCaseSummaryDto(c.Id, c.OrderId, c.OrderNumber, ReturnStatuses.CodeOf(c.StatusId), c.Lines, c.Decided, c.InsrDte, c.CompletedAt))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One case with its lines and who did what and when (RQC-11): two queries however many lines.</summary>
public sealed record GetReturnCaseQuery(long Id) : IRequest<ReturnCaseDetailDto?>;

public sealed class GetReturnCaseQueryHandler(IReturnsDbContext dbContext) : IRequestHandler<GetReturnCaseQuery, ReturnCaseDetailDto?>
{
    public async Task<ReturnCaseDetailDto?> Handle(GetReturnCaseQuery request, CancellationToken cancellationToken)
    {
        var found = await dbContext.Cases.AsNoTracking().Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        return found is null ? null : ReturnCaseMapper.ToDetail(found);
    }
}
