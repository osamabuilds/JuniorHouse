using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Vendors;

/// <summary>
/// A vendor's scorecard (FR-SC-01, spec AC-35..AC-37, AC-39): all-time measures and the rolling window, each with the counts
/// behind it. Two aggregate queries however many deliveries and inspections exist (no per-row work). Unknown vendor: not found.
/// Contains no PO notes, costs or evidence (AC-45).
/// </summary>
public sealed record GetVendorScorecardQuery(long VendorId) : IRequest<VendorScorecard>;

public sealed class GetVendorScorecardQueryHandler(IVendorDbContext dbContext, ScorecardOptions options, TimeProvider timeProvider)
    : IRequestHandler<GetVendorScorecardQuery, VendorScorecard>
{
    public async Task<VendorScorecard> Handle(GetVendorScorecardQuery request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Vendors.AsNoTracking().AnyAsync(v => v.Id == request.VendorId, cancellationToken))
        {
            throw new KeyNotFoundException($"Vendor {request.VendorId} was not found.");
        }

        var cutoff = ScorecardDates.PakistanDate(timeProvider.GetUtcNow()).AddDays(-options.WindowDays);
        var vendorId = request.VendorId;

        var deliveries = await dbContext.ScorecardDeliveries.AsNoTracking()
            .Where(d => d.VendorId == vendorId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Counted = g.Count(),
                OnTime = g.Count(d => d.IsOnTime && !d.IsRompFabricDelay),
                OnQuantity = g.Count(d => d.IsOnQuantity),
                Excluded = g.Count(d => d.IsRompFabricDelay),
                RCounted = g.Count(d => d.DispatchDate >= cutoff),
                ROnTime = g.Count(d => d.DispatchDate >= cutoff && d.IsOnTime && !d.IsRompFabricDelay),
                ROnQuantity = g.Count(d => d.DispatchDate >= cutoff && d.IsOnQuantity),
                RExcluded = g.Count(d => d.DispatchDate >= cutoff && d.IsRompFabricDelay),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var inspections = await dbContext.ScorecardInspections.AsNoTracking()
            .Where(i => i.VendorId == vendorId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Inspected = g.Sum(i => i.InspectedQty),
                Defective = g.Sum(i => i.DefectiveQty),
                RInspected = g.Where(i => i.InspectionDate >= cutoff).Sum(i => i.InspectedQty),
                RDefective = g.Where(i => i.InspectionDate >= cutoff).Sum(i => i.DefectiveQty),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var all = new ScorecardCounts(
            deliveries?.Counted ?? 0, deliveries?.OnTime ?? 0, deliveries?.OnQuantity ?? 0, deliveries?.Excluded ?? 0,
            inspections?.Inspected ?? 0, inspections?.Defective ?? 0);
        var rolling = new ScorecardCounts(
            deliveries?.RCounted ?? 0, deliveries?.ROnTime ?? 0, deliveries?.ROnQuantity ?? 0, deliveries?.RExcluded ?? 0,
            inspections?.RInspected ?? 0, inspections?.RDefective ?? 0);

        var allMeasures = ScorecardCalculator.Calculate(all);
        var rollingMeasures = ScorecardCalculator.Calculate(rolling);
        return new VendorScorecard(
            vendorId, options.WindowDays, all.DeliveriesCounted, all.DeliveriesOnTime, all.DeliveriesOnQuantity, all.DeliveriesExcludedRompDelay,
            all.InspectedUnits, all.DefectiveUnits,
            allMeasures.OnTimePct, allMeasures.OnQuantityPct, allMeasures.DefectRatePct,
            rollingMeasures.OnTimePct, rollingMeasures.OnQuantityPct, rollingMeasures.DefectRatePct);
    }
}
