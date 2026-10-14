namespace Romp.Modules.Quality.Contracts;

/// <summary>Quantity cleared for sale by QC. <c>OutcomeCode</c> is Accepted or Concession (the tag travels with the stock).</summary>
public sealed record ReleasedLine(long BatchId, long SubBatchId, short SizeId, short ColourId, int Qty, string OutcomeCode);

/// <summary>
/// QC's public query surface (ADR 0002): later modules (SKU-GEN, Sprint 5) read released quantity here and never
/// touch QC tables. Rejected, Rework, Pending and undecided quantity is never returned (AC-24, AC-25).
/// </summary>
public interface IQualityQueries
{
    Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesAsync(long poId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesByBatchAsync(long batchId, CancellationToken cancellationToken);

    /// <summary>Counted-so-far per size and colour for a PO across all its GRNs.</summary>
    Task<IReadOnlyList<QcLine>> GetReceivedQuantitiesAsync(long poId, CancellationToken cancellationToken);
}
