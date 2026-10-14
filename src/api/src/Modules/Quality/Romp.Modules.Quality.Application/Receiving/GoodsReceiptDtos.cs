using MediatR;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Receiving;

namespace Romp.Modules.Quality.Application.Receiving;

public sealed record CountedLineDto(short SizeId, short ColourId, int Qty);

public sealed record GrnLineDto(short SizeId, short ColourId, int ClaimedQty, int CountedQty, int DiffQty, bool IsExcess);

/// <summary>The full goods receipt. Built from QC's own data only: no PO notes, costs or target costs (AC-45).</summary>
public sealed record GoodsReceiptDto(
    long Id,
    string GrnNo,
    long DeliveryId,
    long RunId,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly ReceivedDate,
    short ReconciliationId,
    string ReconciliationCode,
    int ClaimedTotal,
    int CountedTotal,
    int DiffTotal,
    string ActingPerson,
    string? Note,
    IReadOnlyList<GrnLineDto> Lines);

/// <summary>One row of the goods receipt list; <c>BatchId</c> and <c>BatchStatusId</c> come from QC's own batch row.</summary>
public sealed record GrnSummaryDto(
    long Id,
    string GrnNo,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly ReceivedDate,
    short ReconciliationId,
    int ClaimedTotal,
    int CountedTotal,
    long? BatchId,
    short? BatchStatusId);

/// <summary>Raised inside the goods receipt's transaction once the receipt is saved; the QC batch is opened by its handler (AC-12).</summary>
public sealed record GoodsReceiptRecordedNotification(long GrnId) : INotification;

internal static class GoodsReceiptMapper
{
    public static GoodsReceiptDto ToDto(this GoodsReceipt grn) => new(
        grn.Id, grn.GrnNo, grn.DeliveryId, grn.RunId, grn.PoId, grn.PoNo, grn.VendorDnNo, grn.ReceivedDate,
        grn.ReconciliationId, GrnReconciliation.Code(grn.ReconciliationId), grn.ClaimedTotal, grn.CountedTotal,
        grn.CountedTotal - grn.ClaimedTotal, grn.ActingPerson, grn.Note,
        grn.Lines.OrderBy(l => l.SizeId).ThenBy(l => l.ColourId)
            .Select(l => new GrnLineDto(l.SizeId, l.ColourId, l.ClaimedQty, l.CountedQty, l.DiffQty, l.IsExcess)).ToList());
}
