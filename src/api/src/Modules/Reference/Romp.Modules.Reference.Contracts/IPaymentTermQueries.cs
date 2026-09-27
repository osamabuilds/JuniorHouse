namespace Romp.Modules.Reference.Contracts;

/// <summary>
/// The only way another module (VNDR, to default a PO's advance % from its vendor's payment term -
/// AC-7a) may read REF data it doesn't already own a copy of, per ADR 0002. Implemented in
/// Romp.Modules.Reference.Infrastructure.
/// </summary>
public interface IPaymentTermQueries
{
    Task<decimal?> GetDefaultAdvancePercentAsync(short paymentTermId, CancellationToken cancellationToken);
}
