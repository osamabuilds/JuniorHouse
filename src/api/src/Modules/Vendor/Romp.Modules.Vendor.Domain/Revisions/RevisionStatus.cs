
namespace Romp.Modules.Vendor.Domain.Revisions;

/// <summary>Mirrors REF.PO_REV_STS_LKP's seeded IDs (Reference module migration 20260928112415_Sprint2Lookups) - same pattern as <see cref="PoStatus"/>.</summary>
internal static class RevisionStatus
{
    public const short Pending = 1;
    public const short InForce = 2;
    public const short Superseded = 3;
    public const short Rejected = 4;
    public const short Withdrawn = 5;
}
