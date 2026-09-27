namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// Maps to VNDR.PO_NO_SEQ (ADR 0006) - the atomic per-year counter behind <see cref="PoNumberAllocator"/>.
/// Internal allocation primitive, not business data, so it's exempt from the audit-column rule
/// (plan.md) and lives in Infrastructure rather than Domain.
/// </summary>
internal sealed class PoNumberSequence
{
    public short Year { get; init; }

    public int Seq { get; init; }
}
