using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Vendors;

/// <summary>"Configuration, not constants" (spec Q2, AC-46): the rolling window behind the scorecard, bound from <c>Vndr:Scorecard:*</c>.</summary>
public sealed class ScorecardOptions
{
    public int WindowDays { get; init; } = 90;
}

internal static class ScorecardDates
{
    // Pakistan has no daylight saving, so a fixed +05:00 offset is exact and needs no tzdata.
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

    public static DateOnly PakistanDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(PakistanOffset).DateTime);
}

/// <summary>
/// Records the delivery fact behind the scorecard when PROD records a delivery note (spec AC-35, AC-36, AC-38). The vendor is
/// resolved from the PO inside VNDR. Idempotent by (PO, DN number); a delivery for an unknown PO is ignored.
/// <c>TimingCode</c> is the PROD lookup code (OnTime, Late, BeyondAcceptable); <c>QuantityCode</c> is OnQuantity, Short or Over.
/// </summary>
public sealed record RecordScorecardDeliveryCommand(
    long PoId,
    string VendorDnNo,
    DateOnly DispatchDate,
    string TimingCode,
    string QuantityCode,
    bool RompFabricDelay) : IRequest<bool>, IVendorCommand;

public sealed class RecordScorecardDeliveryCommandHandler(IVendorDbContext dbContext) : IRequestHandler<RecordScorecardDeliveryCommand, bool>
{
    public async Task<bool> Handle(RecordScorecardDeliveryCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await dbContext.PurchaseOrders.AsNoTracking().Where(p => p.Id == request.PoId).Select(p => (long?)p.VendorId).FirstOrDefaultAsync(cancellationToken);
        if (vendorId is null
            || await dbContext.ScorecardDeliveries.AnyAsync(d => d.PoId == request.PoId && d.VendorDnNo == request.VendorDnNo, cancellationToken))
        {
            return false;
        }

        dbContext.ScorecardDeliveries.Add(new ScorecardDelivery(
            vendorId.Value, request.PoId, request.VendorDnNo, request.DispatchDate,
            onTime: request.TimingCode == "OnTime", onQuantity: request.QuantityCode == "OnQuantity", request.RompFabricDelay));
        return true;
    }
}

/// <summary>
/// Records the inspection fact behind the defect rate when QC records an inspection (spec AC-35, AC-38). Only a batch's first
/// pass counts (Q2 default: whole batch, round 1), so a re-inspection after rework never changes the rate. Idempotent by batch.
/// </summary>
public sealed record RecordScorecardInspectionCommand(
    long PoId,
    long BatchId,
    long SubBatchId,
    short RoundNo,
    int InspectedQty,
    int DefectiveQty,
    DateTimeOffset OccurredAt) : IRequest<bool>, IVendorCommand;

public sealed class RecordScorecardInspectionCommandHandler(IVendorDbContext dbContext) : IRequestHandler<RecordScorecardInspectionCommand, bool>
{
    public async Task<bool> Handle(RecordScorecardInspectionCommand request, CancellationToken cancellationToken)
    {
        if (request.SubBatchId != 0 || request.RoundNo != 1)
        {
            return false; // a re-inspection of reworked goods
        }

        var vendorId = await dbContext.PurchaseOrders.AsNoTracking().Where(p => p.Id == request.PoId).Select(p => (long?)p.VendorId).FirstOrDefaultAsync(cancellationToken);
        if (vendorId is null || await dbContext.ScorecardInspections.AnyAsync(i => i.BatchId == request.BatchId, cancellationToken))
        {
            return false;
        }

        dbContext.ScorecardInspections.Add(new ScorecardInspection(
            vendorId.Value, request.PoId, request.BatchId, ScorecardDates.PakistanDate(request.OccurredAt), request.InspectedQty, request.DefectiveQty));
        return true;
    }
}
