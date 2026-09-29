using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain.Inspections;

namespace Romp.Modules.Quality.Domain.Batches;

/// <summary>Maps to QC.QC_BTCH_MAIN: the lot to inspect, one per goods receipt (Q8). Holds its sub-batches and inspection rounds.</summary>
public sealed class QcBatch : AggregateRoot<long>, IAuditable
{
    private readonly List<QcBatchLine> _lines = [];
    private readonly List<QcSubBatch> _subBatches = [];
    private readonly List<Inspection> _inspections = [];

    private QcBatch()
    {
        PoNo = string.Empty;
    }

    public long GrnId { get; private set; }

    public long PoId { get; private set; }

    public string PoNo { get; private set; }

    public short StatusId { get; private set; }

    /// <summary>The lot size: the sum of the counted lines.</summary>
    public int LotQty { get; private set; }

    public IReadOnlyCollection<QcBatchLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<QcSubBatch> SubBatches => _subBatches.AsReadOnly();

    public IReadOnlyCollection<Inspection> Inspections => _inspections.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_BTCH_LINE: the counted quantity of one size and colour in the batch.</summary>
public sealed class QcBatchLine : Entity<long>, IAuditable
{
    private QcBatchLine()
    {
    }

    public long BatchId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// Maps to QC.QC_BTCH_SPLT: a decided part of a batch. A whole-batch decision is one sub-batch (number 1). The
/// outcome is empty until it is decided.
/// </summary>
public sealed class QcSubBatch : Entity<long>, IAuditable
{
    private readonly List<QcSubBatchLine> _lines = [];
    private readonly List<ReworkReturn> _returns = [];

    private QcSubBatch()
    {
    }

    public long BatchId { get; private set; }

    public short SplitNo { get; private set; }

    public short? OutcomeId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? ActingPerson { get; private set; }

    /// <summary>How many times this part has been sent back for rework (rounds are unlimited).</summary>
    public short ReworkRoundCount { get; private set; }

    public short StatusId { get; private set; }

    public IReadOnlyCollection<QcSubBatchLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<ReworkReturn> Returns => _returns.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_BTCH_SPLT_LINE. <see cref="ReturnedQty"/> is the running total returned from the vendor after rework.</summary>
public sealed class QcSubBatchLine : Entity<long>, IAuditable
{
    private QcSubBatchLine()
    {
    }

    public long SubBatchId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public int ReturnedQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_RWRK_RTRN: one return of reworked goods from the vendor.</summary>
public sealed class ReworkReturn : Entity<long>, IAuditable
{
    private ReworkReturn()
    {
        ActingPerson = string.Empty;
    }

    public long SubBatchId { get; private set; }

    public DateOnly ReturnDate { get; private set; }

    public string ActingPerson { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
