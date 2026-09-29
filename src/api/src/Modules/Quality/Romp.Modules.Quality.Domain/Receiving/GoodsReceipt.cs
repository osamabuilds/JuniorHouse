using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain.Receiving;

/// <summary>Maps to QC.GRN_MAIN: a goods receipt note recorded against one inbound delivery (FR-SC-04, BRD 5.11).</summary>
public sealed class GoodsReceipt : AggregateRoot<long>, IAuditable
{
    private readonly List<GoodsReceiptLine> _lines = [];

    private GoodsReceipt()
    {
        GrnNo = string.Empty;
        PoNo = string.Empty;
        VendorDnNo = string.Empty;
        ActingPerson = string.Empty;
    }

    public string GrnNo { get; private set; }

    public long DeliveryId { get; private set; }

    public long RunId { get; private set; }

    public long PoId { get; private set; }

    public string PoNo { get; private set; }

    public string VendorDnNo { get; private set; }

    public DateOnly ReceivedDate { get; private set; }

    public short StatusId { get; private set; }

    public short ReconciliationId { get; private set; }

    public int ClaimedTotal { get; private set; }

    public int CountedTotal { get; private set; }

    public string ActingPerson { get; private set; }

    public string? Note { get; private set; }

    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.GRN_LINE: claimed against counted for one size and colour.</summary>
public sealed class GoodsReceiptLine : Entity<long>, IAuditable
{
    private GoodsReceiptLine()
    {
    }

    public long GrnId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int ClaimedQty { get; private set; }

    public int CountedQty { get; private set; }

    /// <summary>Counted minus claimed: negative is short, positive is over.</summary>
    public int DiffQty { get; private set; }

    public bool IsExcess { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.GRN_NO_SEQ: the per-year counter behind <c>GRN-YYYY-NNNNN</c> (ADR 0006), private to QC.</summary>
public sealed class GrnNumberSequence
{
    private GrnNumberSequence()
    {
    }

    public short Year { get; private set; }

    public int LastSeq { get; private set; }
}
