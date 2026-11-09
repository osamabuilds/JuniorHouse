using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Fulfilment.Domain.PackStation;

/// <summary>
/// The weighed parcel (FULF.FULF_PACK, FR-SC-12): the weight in grams, the weight expected from the order, the packaging recommended, whether the
/// two differ by more than the tolerance, and the packer's note when they do (PAK-5, PAK-7). One per order; weighing again replaces it.
/// </summary>
public sealed class PackRecord : Entity<long>, IAuditable
{
    public const int MaxNoteLength = 200;

    private PackRecord()
    {
        WeighedBy = string.Empty;
        InsrBy = string.Empty;
    }

    internal PackRecord(int weightGrams, int expectedGrams, short packagingKindId, bool isDeviation, string weighedBy, DateTimeOffset weighedAt)
    {
        WeightGrams = weightGrams;
        ExpectedGrams = expectedGrams;
        PackagingKindId = packagingKindId;
        IsDeviation = isDeviation;
        WeighedBy = weighedBy;
        WeighedAt = weighedAt;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public int WeightGrams { get; private set; }

    public int ExpectedGrams { get; private set; }

    public short PackagingKindId { get; private set; }

    /// <summary>The weight differs from the expected weight by more than the tolerance: flagged for staff (PAK-7).</summary>
    public bool IsDeviation { get; private set; }

    public string? Note { get; private set; }

    public string WeighedBy { get; private set; }

    public DateTimeOffset WeighedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Weighing again replaces the earlier weight (and clears its note): one record per order.</summary>
    internal void Reweigh(int weightGrams, int expectedGrams, short packagingKindId, bool isDeviation, string weighedBy, DateTimeOffset weighedAt)
    {
        WeightGrams = weightGrams;
        ExpectedGrams = expectedGrams;
        PackagingKindId = packagingKindId;
        IsDeviation = isDeviation;
        Note = null;
        WeighedBy = weighedBy;
        WeighedAt = weighedAt;
    }

    internal void AddNote(string note)
    {
        var trimmed = note.Trim();
        if (trimmed.Length is 0 or > MaxNoteLength)
        {
            throw new DomainException($"The note must be 1 to {MaxNoteLength} characters.");
        }

        Note = trimmed;
    }
}

/// <summary>
/// A compact record of one scan (FULF.FULF_SCAN, NFR-SC-05): who scanned what at which station, and whether it was accepted. Not audited (it would
/// double every scan) and purged after 90 days; the partition key, when volume needs it, is the month of the scan.
/// </summary>
public sealed class ScanEntry : Entity<long>
{
    private ScanEntry()
    {
        Code = string.Empty;
        Staff = string.Empty;
        ResultCode = string.Empty;
    }

    public ScanEntry(long orderId, short stageId, string code, bool accepted, string resultCode, string staff, DateTimeOffset at)
    {
        OrderId = orderId;
        StageId = stageId;
        Code = code.Length > 40 ? code[..40] : code;
        OutcomeId = accepted ? ScanOutcomes.Accepted : ScanOutcomes.Refused;
        ResultCode = resultCode;
        Staff = staff;
        At = at;
    }

    public long OrderId { get; private set; }

    public short StageId { get; private set; }

    public string Code { get; private set; }

    public short OutcomeId { get; private set; }

    public string ResultCode { get; private set; }

    public string Staff { get; private set; }

    public DateTimeOffset At { get; private set; }
}
