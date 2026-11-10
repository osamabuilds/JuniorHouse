using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.CodReconciliation;

namespace Romp.Modules.Shipping.Application.CodReconciliation;

public sealed record StatementLineInput(string? Waybill, decimal Amount);

public sealed record CodRemittanceSummaryDto(long Id, string CourierName, string Reference, DateOnly RemittedOn, decimal StatedTotal, int LineCount, int FlaggedCount, int OpenCount, DateTimeOffset RecordedAt, string RecordedBy);

public sealed record CodLineDto(
    long Id, string Waybill, decimal Amount, string Outcome, string OutcomeName, string? OrderNumber, decimal? ExpectedCash, string? ResolutionNote, string? ResolvedBy, DateTimeOffset? ResolvedAt, bool IsOpen);

public sealed record CodRemittanceDetailDto(CodRemittanceSummaryDto Summary, IReadOnlyList<CodLineDto> Lines);

public sealed record RecordCodResult(CodRemittanceDetailDto Remittance, bool AlreadyRecorded);

/// <summary>
/// Records a courier's cash statement (COD-2 to COD-10, COD-16): the statement is checked as typed, every line is judged against the parcel it names, and
/// matching parcels are settled, all in one transaction. The same reference from the same courier again changes nothing and answers with the first (COD-9).
/// </summary>
public sealed record RecordCodRemittanceCommand(string CourierCode, string? Reference, DateOnly RemittedOn, decimal StatedTotal, IReadOnlyList<StatementLineInput> Lines, string Staff)
    : IRequest<RecordCodResult>, IShippingCommand;

public sealed class RecordCodRemittanceCommandHandler(IShippingDbContext dbContext) : IRequestHandler<RecordCodRemittanceCommand, RecordCodResult>
{
    public async Task<RecordCodResult> Handle(RecordCodRemittanceCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }

        var courierId = Couriers.FromCode(request.CourierCode) ?? throw new DomainException("Choose a courier.");
        var (reference, lines) = CodRemittance.Validate(request.Reference, request.StatedTotal, [.. request.Lines.Select(l => new CodStatementLine(l.Waybill ?? string.Empty, l.Amount))]);

        var existing = await dbContext.CodRemittances.AsNoTracking().Where(r => r.CourierId == courierId && r.Reference == reference).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is { } id)
        {
            return new RecordCodResult((await CodReader.DetailAsync(dbContext, id, cancellationToken))!, true);
        }

        var waybills = lines.Select(l => l.Waybill).Distinct().ToList();
        var parcels = await dbContext.Shipments.Where(s => s.CourierId == courierId && s.Waybill != null && waybills.Contains(s.Waybill)).ToDictionaryAsync(s => s.Waybill!, cancellationToken);

        var remittance = CodRemittance.Record(courierId, reference, request.RemittedOn, request.StatedTotal);
        foreach (var line in lines)
        {
            parcels.TryGetValue(line.Waybill, out var parcel);
            var outcome = CodJudge.Judge(parcel is null ? null : new CodParcelFacts(parcel.DeliveredAt is not null, parcel.CashToCollect, parcel.CodStatusId), line.Amount);
            remittance.AddLine(line.Waybill, line.Amount, parcel?.Id, outcome);

            // A later line for the same parcel in this statement is then a duplicate, because the first already changed the parcel (COD-4).
            parcel?.ApplyRemittance(outcome);
        }

        dbContext.CodRemittances.Add(remittance);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecordCodResult((await CodReader.DetailAsync(dbContext, remittance.Id, cancellationToken))!, false);
    }
}

/// <summary>Staff close a flagged line with a note (COD-12). A short or over parcel becomes resolved.</summary>
public sealed record ResolveCodLineCommand(long LineId, string? Note, string Staff) : IRequest<CodLineDto?>, IShippingCommand;

public sealed class ResolveCodLineCommandHandler(IShippingDbContext dbContext, TimeProvider clock) : IRequestHandler<ResolveCodLineCommand, CodLineDto?>
{
    public async Task<CodLineDto?> Handle(ResolveCodLineCommand request, CancellationToken cancellationToken)
    {
        var line = await dbContext.CodRemittanceLines.FirstOrDefaultAsync(l => l.Id == request.LineId, cancellationToken);
        if (line is null)
        {
            return null;
        }

        line.Resolve(request.Note ?? string.Empty, request.Staff, clock.GetUtcNow());
        if (line.ShipmentId is { } shipmentId && line.OutcomeId is RemittanceOutcomes.ShortPaid or RemittanceOutcomes.Over)
        {
            (await dbContext.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken))?.ResolveCod();
        }

        return new CodLineDto(line.Id, line.Waybill, line.Amount, RemittanceOutcomes.CodeOf(line.OutcomeId), RemittanceOutcomes.NameOf(line.OutcomeId), null, null, line.ResolutionNote, line.ResolvedBy, line.ResolvedAt, line.IsOpen);
    }
}

/// <summary>The daily job: delivered cash parcels unpaid after the cycle are flagged overdue, once each (COD-11). Returns how many were flagged.</summary>
public sealed record FlagOverdueCodCommand : IRequest<int>, IShippingCommand;

public sealed class FlagOverdueCodCommandHandler(IShippingDbContext dbContext, IOptions<ShippingOptions> options, TimeProvider clock) : IRequestHandler<FlagOverdueCodCommand, int>
{
    public async Task<int> Handle(FlagOverdueCodCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.CodRemitDays);
        var due = await dbContext.Shipments
            .Where(s => s.CodStatusId == CodStatuses.Awaiting && s.CodOverdueAt == null && s.DeliveredAt != null && s.DeliveredAt <= cutoff)
            .OrderBy(s => s.Id).Take(500).ToListAsync(cancellationToken);
        return due.Count(s => s.FlagCodOverdue(now, options.Value.CodRemitDays));
    }
}

public sealed record CourierCodTotalsDto(string CourierName, int AwaitingCount, decimal AwaitingCash, int OverdueCount, decimal OverdueCash, int SettledCount, decimal SettledCash);

public sealed record CodSummaryDto(IReadOnlyList<CourierCodTotalsDto> Couriers, int OpenExceptions);

/// <summary>The reconciliation overview (COD-13): per courier what is awaiting, overdue and settled, plus the open exceptions. Two grouped queries.</summary>
public sealed record GetCodSummaryQuery : IRequest<CodSummaryDto>;

public sealed class GetCodSummaryQueryHandler(IShippingDbContext dbContext) : IRequestHandler<GetCodSummaryQuery, CodSummaryDto>
{
    public async Task<CodSummaryDto> Handle(GetCodSummaryQuery request, CancellationToken cancellationToken)
    {
        var groups = await dbContext.Shipments.AsNoTracking()
            .Where(s => s.CourierId != null && s.DeliveredAt != null && s.CodStatusId != CodStatuses.NotCod)
            .GroupBy(s => new { s.CourierId, s.CodStatusId, Overdue = s.CodOverdueAt != null })
            .Select(g => new { g.Key.CourierId, g.Key.CodStatusId, g.Key.Overdue, Count = g.Count(), Cash = g.Sum(s => s.CashToCollect) })
            .ToListAsync(cancellationToken);
        var open = await dbContext.CodRemittanceLines.AsNoTracking().CountAsync(l => l.OutcomeId != RemittanceOutcomes.Matched && l.ResolvedAt == null, cancellationToken);

        var couriers = groups.GroupBy(g => g.CourierId!.Value).OrderBy(g => g.Key).Select(g => new CourierCodTotalsDto(
            Couriers.NameOf(g.Key),
            g.Where(x => x.CodStatusId == CodStatuses.Awaiting).Sum(x => x.Count),
            g.Where(x => x.CodStatusId == CodStatuses.Awaiting).Sum(x => x.Cash),
            g.Where(x => x.CodStatusId == CodStatuses.Awaiting && x.Overdue).Sum(x => x.Count),
            g.Where(x => x.CodStatusId == CodStatuses.Awaiting && x.Overdue).Sum(x => x.Cash),
            g.Where(x => x.CodStatusId == CodStatuses.Settled).Sum(x => x.Count),
            g.Where(x => x.CodStatusId == CodStatuses.Settled).Sum(x => x.Cash))).ToList();
        return new CodSummaryDto(couriers, open);
    }
}

public sealed record CodAwaitingRowDto(long ShipmentId, string OrderNumber, string Waybill, string CourierName, decimal CashToCollect, DateTimeOffset DeliveredAt, int DaysWaiting, bool Overdue);

/// <summary>Delivered cash parcels still unpaid, oldest first, optionally only the overdue ones (COD-13, COD-14).</summary>
public sealed record ListCodAwaitingQuery(bool OverdueOnly = false, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<CodAwaitingRowDto>>;

public sealed class ListCodAwaitingQueryHandler(IShippingDbContext dbContext, TimeProvider clock) : IRequestHandler<ListCodAwaitingQuery, PagedResult<CodAwaitingRowDto>>
{
    public async Task<PagedResult<CodAwaitingRowDto>> Handle(ListCodAwaitingQuery request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var query = dbContext.Shipments.AsNoTracking().Where(s => s.CodStatusId == CodStatuses.Awaiting && s.DeliveredAt != null && s.CourierId != null);
        if (request.OverdueOnly)
        {
            query = query.Where(s => s.CodOverdueAt != null);
        }

        var page = await query.OrderBy(s => s.DeliveredAt).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.OrderNumber, s.Waybill, s.CourierId, s.CashToCollect, s.DeliveredAt, Overdue = s.CodOverdueAt != null })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<CodAwaitingRowDto>(
            [.. page.Items.Select(s => new CodAwaitingRowDto(s.Id, s.OrderNumber, s.Waybill ?? string.Empty, Couriers.NameOf(s.CourierId!.Value), s.CashToCollect, s.DeliveredAt!.Value, (int)(now - s.DeliveredAt.Value).TotalDays, s.Overdue))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed record CodExceptionRowDto(long LineId, long RemittanceId, string Reference, string CourierName, string Waybill, decimal Amount, string Outcome, string OutcomeName, string? OrderNumber, decimal? ExpectedCash);

/// <summary>Flagged statement lines nobody has resolved yet, oldest first (COD-12, COD-14). One page query plus one for the parcels and statements.</summary>
public sealed record ListCodExceptionsQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<CodExceptionRowDto>>;

public sealed class ListCodExceptionsQueryHandler(IShippingDbContext dbContext) : IRequestHandler<ListCodExceptionsQuery, PagedResult<CodExceptionRowDto>>
{
    public async Task<PagedResult<CodExceptionRowDto>> Handle(ListCodExceptionsQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.CodRemittanceLines.AsNoTracking().Where(l => l.OutcomeId != RemittanceOutcomes.Matched && l.ResolvedAt == null)
            .OrderBy(l => l.Id).Select(l => new { l.Id, l.RemittanceId, l.Waybill, l.Amount, l.OutcomeId, l.ShipmentId })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        var remittanceIds = page.Items.Select(l => l.RemittanceId).Distinct().ToList();
        var shipmentIds = page.Items.Where(l => l.ShipmentId != null).Select(l => l.ShipmentId!.Value).Distinct().ToList();
        var remittances = await dbContext.CodRemittances.AsNoTracking().Where(r => remittanceIds.Contains(r.Id)).Select(r => new { r.Id, r.Reference, r.CourierId }).ToDictionaryAsync(r => r.Id, cancellationToken);
        var parcels = await dbContext.Shipments.AsNoTracking().Where(s => shipmentIds.Contains(s.Id)).Select(s => new { s.Id, s.OrderNumber, s.CashToCollect }).ToDictionaryAsync(s => s.Id, cancellationToken);
        return new PagedResult<CodExceptionRowDto>(
            [.. page.Items.Select(l => new CodExceptionRowDto(
                l.Id, l.RemittanceId, remittances[l.RemittanceId].Reference, Couriers.NameOf(remittances[l.RemittanceId].CourierId), l.Waybill, l.Amount,
                RemittanceOutcomes.CodeOf(l.OutcomeId), RemittanceOutcomes.NameOf(l.OutcomeId),
                l.ShipmentId is { } sid ? parcels[sid].OrderNumber : null, l.ShipmentId is { } sid2 ? parcels[sid2].CashToCollect : null))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>The recorded statements, newest first, paged (COD-14).</summary>
public sealed record ListCodRemittancesQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<CodRemittanceSummaryDto>>;

public sealed class ListCodRemittancesQueryHandler(IShippingDbContext dbContext) : IRequestHandler<ListCodRemittancesQuery, PagedResult<CodRemittanceSummaryDto>>
{
    public async Task<PagedResult<CodRemittanceSummaryDto>> Handle(ListCodRemittancesQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.CodRemittances.AsNoTracking().OrderByDescending(r => r.RemittedOn).ThenByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id, r.CourierId, r.Reference, r.RemittedOn, r.StatedTotal, r.InsrDte, r.InsrBy,
                Lines = r.Lines.Count,
                Flagged = r.Lines.Count(l => l.OutcomeId != RemittanceOutcomes.Matched),
                Open = r.Lines.Count(l => l.OutcomeId != RemittanceOutcomes.Matched && l.ResolvedAt == null),
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<CodRemittanceSummaryDto>(
            [.. page.Items.Select(r => new CodRemittanceSummaryDto(r.Id, Couriers.NameOf(r.CourierId), r.Reference, r.RemittedOn, r.StatedTotal, r.Lines, r.Flagged, r.Open, r.InsrDte, r.InsrBy))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One statement with every line and the parcel each points at: three queries however many lines.</summary>
public sealed record GetCodRemittanceQuery(long Id) : IRequest<CodRemittanceDetailDto?>;

public sealed class GetCodRemittanceQueryHandler(IShippingDbContext dbContext) : IRequestHandler<GetCodRemittanceQuery, CodRemittanceDetailDto?>
{
    public Task<CodRemittanceDetailDto?> Handle(GetCodRemittanceQuery request, CancellationToken cancellationToken) => CodReader.DetailAsync(dbContext, request.Id, cancellationToken);
}

internal static class CodReader
{
    public static async Task<CodRemittanceDetailDto?> DetailAsync(IShippingDbContext dbContext, long id, CancellationToken cancellationToken)
    {
        var remittance = await dbContext.CodRemittances.AsNoTracking().Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (remittance is null)
        {
            return null;
        }

        var shipmentIds = remittance.Lines.Where(l => l.ShipmentId != null).Select(l => l.ShipmentId!.Value).Distinct().ToList();
        var parcels = await dbContext.Shipments.AsNoTracking().Where(s => shipmentIds.Contains(s.Id)).Select(s => new { s.Id, s.OrderNumber, s.CashToCollect }).ToDictionaryAsync(s => s.Id, cancellationToken);
        var lines = remittance.Lines.OrderBy(l => l.Id).Select(l => new CodLineDto(
            l.Id, l.Waybill, l.Amount, RemittanceOutcomes.CodeOf(l.OutcomeId), RemittanceOutcomes.NameOf(l.OutcomeId),
            l.ShipmentId is { } a ? parcels[a].OrderNumber : null, l.ShipmentId is { } b ? parcels[b].CashToCollect : null,
            l.ResolutionNote, l.ResolvedBy, l.ResolvedAt, l.IsOpen)).ToList();
        var summary = new CodRemittanceSummaryDto(
            remittance.Id, Couriers.NameOf(remittance.CourierId), remittance.Reference, remittance.RemittedOn, remittance.StatedTotal, lines.Count,
            lines.Count(l => l.Outcome != "MATCHED"), lines.Count(l => l.IsOpen), remittance.InsrDte, remittance.InsrBy);
        return new CodRemittanceDetailDto(summary, lines);
    }
}
