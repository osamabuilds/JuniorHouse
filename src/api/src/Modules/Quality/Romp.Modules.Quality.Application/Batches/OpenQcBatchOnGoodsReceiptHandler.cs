using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Receiving;
using Romp.Modules.Quality.Domain.Batches;

namespace Romp.Modules.Quality.Application.Batches;

/// <summary>
/// Opens the QC batch for a goods receipt, inside the receipt's own transaction (spec AC-12, plan "in-process notification
/// between C and D"): one batch per GRN (Q8), status Pending, lines equal to the counted quantities. Idempotent. A receipt
/// that counted nothing has nothing to inspect and gets no batch.
/// </summary>
public sealed class OpenQcBatchOnGoodsReceiptHandler(IQualityDbContext dbContext)
    : INotificationHandler<GoodsReceiptRecordedNotification>
{
    public async Task Handle(GoodsReceiptRecordedNotification notification, CancellationToken cancellationToken)
    {
        if (await dbContext.Batches.AnyAsync(b => b.GrnId == notification.GrnId, cancellationToken))
        {
            return;
        }

        var grn = await dbContext.GoodsReceipts.AsNoTracking().Include(g => g.Lines).SingleAsync(g => g.Id == notification.GrnId, cancellationToken);
        var batch = QcBatch.OpenFor(grn.Id, grn.PoId, grn.PoNo, grn.Lines.Select(l => (l.SizeId, l.ColourId, l.CountedQty)));
        if (batch is not null)
        {
            dbContext.Batches.Add(batch);
        }
    }
}
