namespace Romp.Modules.Vendor.Application;

/// <summary>
/// Allocates the next <c>PO-{YYYY}-{NNNNN}</c> number for the current calendar year, atomically
/// (ADR 0006) - safe under concurrent PO creation (AC-7b). Implemented in Infrastructure, which is
/// the only layer allowed to run raw SQL against <c>VNDR.PO_NO_SEQ</c>.
/// </summary>
public interface IPoNumberAllocator
{
    Task<string> AllocateAsync(CancellationToken cancellationToken);
}
