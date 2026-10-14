namespace Romp.Modules.Production.Contracts;

/// <summary>
/// Sprint 3 (ADR 0008). The read-only questions VNDR may ask PROD when a decision must be
/// consistent at that moment (the amendment guard), per ADR 0002 - it depends on this Contracts
/// project only.
/// </summary>
public interface IProductionQueries
{
    /// <summary>Total shipped so far per size x colour across every delivery note on the PO. Empty when there is no run or no note.</summary>
    Task<IReadOnlyList<DeliveredQuantity>> GetDeliveredQuantitiesAsync(long poId, CancellationToken cancellationToken);

    /// <summary>True once a PP-sample round has been approved for the PO's run.</summary>
    Task<bool> HasApprovedPpSampleAsync(long poId, CancellationToken cancellationToken);
}

public sealed record DeliveredQuantity(short SizeId, short ColourId, int Qty);
