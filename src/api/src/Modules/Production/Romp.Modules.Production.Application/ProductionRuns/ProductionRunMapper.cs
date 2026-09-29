using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Application.ProductionRuns;

internal static class ProductionRunMapper
{
    /// <summary>
    /// Loads a run with every collection the aggregate's rules read (milestones, rounds with files,
    /// history, notes with lines). An aggregate method that looks at a collection this did not load
    /// would silently see it empty - so every command handler loads through here.
    /// </summary>
    public static async Task<ProductionRun> LoadRunAsync(this IProductionDbContext db, long runId, CancellationToken cancellationToken) =>
        await db.Runs.AsSplitQuery()
            .Include(r => r.Milestones).ThenInclude(m => m.FinishedLines)
            .Include(r => r.PpRounds).ThenInclude(p => p.Files)
            .Include(r => r.ExpectedHistory)
            .Include(r => r.DeliveryNotes).ThenInclude(n => n.Lines)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
        ?? throw new KeyNotFoundException($"Production run {runId} was not found.");

    public static MilestoneDto ToDto(this Milestone m) => new(
        m.Id, m.TypeId, MilestoneType.Code(m.TypeId), m.ClaimedDate, m.ReportedBy, m.ActingPerson, m.Note, m.IsRecordedLate, m.FlagReason,
        m.PpRoundId, m.InsrDte, m.InsrBy, m.FinishedLines.Select(l => new QuantityLineDto(l.SizeId, l.ColourId, l.Qty)).ToList());

    public static PpSampleFileDto ToDto(this PpSampleFile f) => new(f.Id, f.FileName, f.ContentType, f.FileSizeBytes, f.InsrDte);

    public static PpSampleRoundDto ToDto(this PpSampleRound r) => new(
        r.Id, r.RoundNo, r.SubmittedDate, r.SubmittedBy, r.Note, r.DecisionId,
        r.DecisionId switch { PpDecision.Approved => "Approved", PpDecision.Rejected => "Rejected", _ => "Pending" },
        r.DecidedAt, r.DecidedBy, r.DecisionReason,
        r.Files.Where(f => !f.IsDeleted).OrderBy(f => f.Id).Select(f => f.ToDto()).ToList());

    public static DeliveryNoteDto ToDto(this DeliveryNote n) => new(
        n.Id, n.VendorDnNo, n.DispatchDate, n.IsFinalShipment, n.RevisionNumber,
        n.TimingId, DeliveryTiming.Code(n.TimingId), n.DaysLate,
        n.QuantityStatusId, DeliveryQuantityStatus.Code(n.QuantityStatusId), n.OverQty, n.ShortQty,
        n.FabricDispatchDate, n.IsRompFabricDelay, n.ActingPerson, n.InsrDte,
        n.Lines.OrderBy(l => l.SizeId).ThenBy(l => l.ColourId).Select(l => new DeliveryNoteLineDto(l.SizeId, l.ColourId, l.Qty, l.OverQty, l.ShortQty)).ToList());

    public static bool CanSubmitSample(this ProductionRun run) =>
        run.IsOpen && !run.PpRounds.Any(r => r.IsPending) && (!run.HasApprovedPpSample || run.IsApprovalSuperseded);
}
