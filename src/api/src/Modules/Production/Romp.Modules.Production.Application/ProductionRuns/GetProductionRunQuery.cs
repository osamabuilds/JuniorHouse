using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Application.ProductionRuns;

/// <summary>Run detail (AC-19, AC-34): timeline, PP rounds, history, delivery notes and ordered-vs-shipped.</summary>
public sealed record GetProductionRunQuery(long RunId) : IRequest<RunDetailDto>;

/// <summary>The same detail, looked up by the PO (the PO screen links to its run). Null when the PO has no run.</summary>
public sealed record GetProductionRunByPoQuery(long PoId) : IRequest<RunDetailDto?>;

public sealed class GetProductionRunQueryHandler(
    IProductionDbContext dbContext, IPurchaseOrderQueries poQueries, ProductionOptions options)
    : IRequestHandler<GetProductionRunQuery, RunDetailDto>, IRequestHandler<GetProductionRunByPoQuery, RunDetailDto?>
{
    public async Task<RunDetailDto> Handle(GetProductionRunQuery request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);
        return await BuildAsync(run, cancellationToken);
    }

    public async Task<RunDetailDto?> Handle(GetProductionRunByPoQuery request, CancellationToken cancellationToken)
    {
        var runId = await dbContext.Runs.AsNoTracking().Where(r => r.PoId == request.PoId).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken);
        return runId is null ? null : await BuildAsync(await dbContext.LoadRunAsync(runId.Value, cancellationToken), cancellationToken);
    }

    private async Task<RunDetailDto> BuildAsync(ProductionRun run, CancellationToken cancellationToken)
    {
        // One contract call for the PO's terms; only the fields a run screen needs are copied out (AC-34).
        var terms = await poQueries.GetInForceTermsAsync(run.PoId, cancellationToken);
        var shipped = run.ShippedToDate();

        var quantities = (terms?.Lines ?? [])
            .OrderBy(l => l.SizeId).ThenBy(l => l.ColourId)
            .Select(l => new OrderedVsShippedDto(
                l.SizeId, l.ColourId, l.Qty, shipped.GetValueOrDefault((l.SizeId, l.ColourId)),
                DeliveryJudge.MinRequired(l.Qty, terms!.UnderTolerancePercent),
                DeliveryJudge.MaxAllowed(l.Qty, terms.OverTolerancePercent)))
            .ToList();

        return new RunDetailDto(
            run.Id, run.PoId, run.PoNo, run.StatusId, run.FabricResponsibilityId, run.PoSentDate,
            run.ExpectedCompletionDate, terms?.LatestAcceptableDate ?? run.LatestAcceptableDate, run.CuttingStartedDate, run.FabricDispatchDate,
            run.IsAtRisk(options.AtRiskBufferDays), run.HasApprovedPpSample,
            run.IsCuttingBeforeFabricDispatch, run.IsResampleRecommended, run.IsCuttingWithoutResample, run.CanSubmitSample(),
            terms is null ? null : new RunPoTermsDto(terms.RevisionNumber, terms.ExpectedDeliveryDate, terms.LatestAcceptableDate, terms.OverTolerancePercent, terms.UnderTolerancePercent),
            run.Milestones.OrderBy(m => m.ClaimedDate).ThenBy(m => m.Id).Select(m => m.ToDto()).ToList(),
            run.PpRounds.OrderBy(r => r.RoundNo).Select(r => r.ToDto()).ToList(),
            run.ExpectedHistory.OrderBy(h => h.Id).Select(h => new ExpectedCompletionChangeDto(h.OldDate, h.NewDate, h.Reason, h.ActingPerson, h.InsrDte)).ToList(),
            run.DeliveryNotes.OrderBy(n => n.DispatchDate).ThenBy(n => n.Id).Select(n => n.ToDto()).ToList(),
            quantities);
    }
}
