namespace Romp.Modules.Warehouse.Contracts;

/// <summary>
/// Returned units to put back on a shelf (ADR 0033). <c>VariantId</c> is Labelling's variant id, as in the stock events. The key makes a
/// repeat restock nothing more; <c>Seconds</c> files the units as concession (graded) stock instead of sellable. No personal data.
/// </summary>
public sealed record RestockRequest(long VariantId, string BinCode, int Quantity, bool Seconds, string IdempotencyKey, string ActingPerson);

/// <summary>Restocked, or refused with a plain reason (unknown or inactive bin, unknown variant). A refusal changes nothing.</summary>
public sealed record RestockResult(bool Restocked, string? Refusal);

/// <summary>How Returns puts inspected units back into stock (RQC-4, RQC-9, RQC-10). Warehouse applies its own posting rules.</summary>
public interface IStockReturns
{
    Task<RestockResult> RestockAsync(RestockRequest request, CancellationToken cancellationToken);
}
