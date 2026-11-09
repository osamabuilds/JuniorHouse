using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Fulfilment.Domain.PickExceptions;

/// <summary>
/// A picker said the shelf does not hold what the system says (FULF.FULF_EXCP, FR-SC-11). It is a record of what was found, by whom and when
/// (PEX-4, PEX-10); stock itself is never overwritten here (PEX-6). It stays open until staff resolve it (PEX-8, PEX-9).
/// </summary>
public sealed class PickExceptionRecord : Entity<long>, IAuditable
{
    private PickExceptionRecord()
    {
        BinCode = string.Empty;
        RaisedBy = string.Empty;
        InsrBy = string.Empty;
    }

    internal PickExceptionRecord(long variantId, long binId, string binCode, short reasonId, int systemQuantity, int foundQuantity, string raisedBy, DateTimeOffset raisedAt)
    {
        VariantId = variantId;
        BinId = binId;
        BinCode = binCode;
        ReasonId = reasonId;
        SystemQuantity = systemQuantity;
        FoundQuantity = foundQuantity;
        RaisedBy = raisedBy;
        RaisedAt = raisedAt;
        StatusId = ExceptionStatuses.Open;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public long VariantId { get; private set; }

    public long BinId { get; private set; }

    public string BinCode { get; private set; }

    public short ReasonId { get; private set; }

    public short StatusId { get; private set; }

    /// <summary>The units the system expected in the bin for this line.</summary>
    public int SystemQuantity { get; private set; }

    /// <summary>The units the picker counted on the shelf.</summary>
    public int FoundQuantity { get; private set; }

    public string RaisedBy { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public string? ResolvedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool? WasRestocked { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void Resolve(string staff, DateTimeOffset at, bool restocked)
    {
        if (string.IsNullOrWhiteSpace(staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }

        if (StatusId == ExceptionStatuses.Resolved)
        {
            throw new DomainException("This exception is already resolved.");
        }

        StatusId = ExceptionStatuses.Resolved;
        ResolvedBy = staff;
        ResolvedAt = at;
        WasRestocked = restocked;
    }
}
