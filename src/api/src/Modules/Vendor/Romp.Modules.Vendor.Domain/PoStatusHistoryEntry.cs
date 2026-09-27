using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>
/// Maps to VNDR.PO_STS_HIST - append-only status-transition ledger (AC-14). No UPDT_*/xmin: once
/// written, a history row is never changed (docs/db/naming.md rule 6's append-only-ledger
/// allowance) - <see cref="UpdtDte"/>/<see cref="UpdtBy"/> exist only to satisfy
/// <see cref="IAuditable"/> so AuditSaveChangesInterceptor stamps INSR_DTE/INSR_BY the same way
/// as every other entity; CatalogDbContext-style config ignores them (they're never mapped to a
/// column, since this table doesn't have one for them).
/// </summary>
public sealed class PoStatusHistoryEntry : Entity<long>, IAuditable
{
    private PoStatusHistoryEntry()
    {
    }

    internal PoStatusHistoryEntry(long poId, short poStatusId, short? cancelReasonId)
    {
        PoId = poId;
        PoStatusId = poStatusId;
        CancelReasonId = cancelReasonId;
    }

    public long PoId { get; private set; }

    public short PoStatusId { get; private set; }

    public short? CancelReasonId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
