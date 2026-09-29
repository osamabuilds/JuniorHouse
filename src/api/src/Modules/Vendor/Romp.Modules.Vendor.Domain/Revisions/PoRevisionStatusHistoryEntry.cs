using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Domain.Revisions;

/// <summary>
/// Maps to VNDR.PO_REV_STS_HIST - append-only revision-status ledger, same pattern as
/// <see cref="PoStatusHistoryEntry"/>. No UPDT_*/xmin (docs/db/naming.md's append-only-ledger
/// allowance).
/// </summary>
public sealed class PoRevisionStatusHistoryEntry : Entity<long>, IAuditable
{
    private PoRevisionStatusHistoryEntry()
    {
    }

    internal PoRevisionStatusHistoryEntry(long revisionId, short? fromStatusId, short toStatusId, string? note)
    {
        RevisionId = revisionId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        Note = note;
    }

    public long RevisionId { get; private set; }

    public short? FromStatusId { get; private set; }

    public short ToStatusId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
