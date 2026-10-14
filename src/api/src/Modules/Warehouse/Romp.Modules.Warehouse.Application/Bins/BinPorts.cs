namespace Romp.Modules.Warehouse.Application.Bins;

/// <summary>A bin as the other warehouse features see it.</summary>
public sealed record BinInfo(long Id, string BinCode, bool IsActive);

/// <summary>
/// Bins' public surface for the other features (plan, one-way dependencies): putaway, adjustments and cycle counts check that a
/// bin exists and is active through this port, never through the bin entity.
/// </summary>
public interface IBinReader
{
    /// <summary>The bin with this code, active or not, or null when no such bin exists. Lookup is by the exact upper-cased code.</summary>
    Task<BinInfo?> FindByCodeAsync(string binCode, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<long, BinInfo>> GetManyAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken);
}

/// <summary>
/// How many units a bin holds. Declared here and implemented by stock-ledger (the one port declared by the caller, so bins never
/// depends on the ledger). One call answers for many bins, so lists never run per row.
/// </summary>
public interface IBinStockQuery
{
    Task<IReadOnlyDictionary<long, int>> GetUnitsOnHandAsync(IReadOnlyCollection<long> binIds, CancellationToken cancellationToken);
}

/// <summary>Cycle counts record when a bin was last counted through this port.</summary>
public interface IBinCountMarker
{
    Task MarkCountedAsync(IReadOnlyCollection<long> binIds, DateTimeOffset countedAt, CancellationToken cancellationToken);
}

public sealed record BinRowDto(long Id, string BinCode, string Zone, string Aisle, string Shelf, bool IsActive, int UnitsOnHand, DateTimeOffset? LastCountedAt);
