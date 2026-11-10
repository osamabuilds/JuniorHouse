using Romp.BuildingBlocks.Domain;
using Romp.Modules.Returns.Contracts;

namespace Romp.Modules.Returns.Domain.ReverseQc;

/// <summary>An order line as it was when the case opened. Only ids, quantities and codes are kept (RQC-13).</summary>
public sealed record ReturnLineSeed(long VariantId, string? Sku, string Name, string Colour, string Size, int Quantity);

/// <summary>One returned line (RTRN.RTRN_LINE) and what reverse QC decided for it. A line is decided once (RQC-6, RQC-9).</summary>
public sealed class ReturnCaseLine : Entity<long>, IAuditable
{
    public const int MaxNoteLength = 200;

    private ReturnCaseLine()
    {
        Name = string.Empty;
        Colour = string.Empty;
        Size = string.Empty;
        InsrBy = string.Empty;
    }

    internal ReturnCaseLine(ReturnLineSeed seed)
    {
        VariantId = seed.VariantId;
        Sku = seed.Sku;
        Name = seed.Name;
        Colour = seed.Colour;
        Size = seed.Size;
        OrderedQuantity = seed.Quantity;
        InsrBy = string.Empty;
    }

    public long CaseId { get; private set; }

    public long VariantId { get; private set; }

    public string? Sku { get; private set; }

    public string Name { get; private set; }

    public string Colour { get; private set; }

    public string Size { get; private set; }

    public int OrderedQuantity { get; private set; }

    public short? DecisionId { get; private set; }

    public int? DecidedQuantity { get; private set; }

    public string? Note { get; private set; }

    public string? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public bool IsDecided => DecisionId is not null;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void Decide(short decisionId, int quantity, string? note, string staff, DateTimeOffset at)
    {
        if (IsDecided)
        {
            throw new DomainException("This line was already decided.");
        }

        if (!ReturnDecisions.IsKnown(decisionId))
        {
            throw new DomainException("Choose Restock, Restock as seconds or Reject.");
        }

        if (quantity < 1 || quantity > OrderedQuantity)
        {
            throw new DomainException($"The quantity is from 1 to {OrderedQuantity}, what the order had.");
        }

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (decisionId == ReturnDecisions.Reject && text is null)
        {
            throw new DomainException($"Say why it is rejected in 1 to {MaxNoteLength} characters.");
        }

        if (text is { Length: > MaxNoteLength })
        {
            throw new DomainException($"The note is at most {MaxNoteLength} characters.");
        }

        DecisionId = decisionId;
        DecidedQuantity = quantity;
        Note = text;
        DecidedBy = staff;
        DecidedAt = at;
    }
}

/// <summary>
/// A returned parcel going through the reverse-QC gate (RTRN.RTRN_MAIN, FR-SC-18, ADR 0033): awaiting receipt, in inspection, completed. Received once;
/// every line decided once; completed means frozen, and only completion announces a result (RQC-8, FR-SC-19).
/// </summary>
public sealed class ReturnCase : AggregateRoot<long>, IAuditable
{
    public const int MaxBinCodeLength = 12;

    private readonly List<ReturnCaseLine> _lines = [];

    private ReturnCase()
    {
        OrderNumber = string.Empty;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public string OrderNumber { get; private set; }

    public short KindId { get; private set; }

    public short StatusId { get; private set; }

    public bool? SealIntact { get; private set; }

    public string? ReceivingBinCode { get; private set; }

    public string? ReceivedBy { get; private set; }

    public DateTimeOffset? ReceivedAt { get; private set; }

    public string? CompletedBy { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<ReturnCaseLine> Lines => _lines;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Opens a case for a returned parcel with the order's lines, awaiting receipt (RQC-1).</summary>
    public static ReturnCase Open(long orderId, string orderNumber, short kindId, IReadOnlyList<ReturnLineSeed> lines)
    {
        if (!ReturnKinds.IsKnown(kindId))
        {
            throw new DomainException("Unknown return kind.");
        }

        if (lines.Count == 0)
        {
            throw new DomainException("A return case needs at least one line.");
        }

        var created = new ReturnCase { OrderId = orderId, OrderNumber = orderNumber, KindId = kindId, StatusId = ReturnStatuses.AwaitingReceipt };
        created._lines.AddRange(lines.Select(l => new ReturnCaseLine(l)));
        return created;
    }

    /// <summary>Records the parcel arriving: seal state and the bin stock goes to. Received once (RQC-2, RQC-3, RQC-9).</summary>
    public void Receive(bool sealIntact, string binCode, string staff, DateTimeOffset at)
    {
        RequireStaff(staff);
        if (StatusId != ReturnStatuses.AwaitingReceipt)
        {
            throw new DomainException("This parcel was already received.");
        }

        var bin = (binCode ?? string.Empty).Trim().ToUpperInvariant();
        if (bin.Length is 0 or > MaxBinCodeLength)
        {
            throw new DomainException($"Give the bin code, 1 to {MaxBinCodeLength} characters.");
        }

        SealIntact = sealIntact;
        ReceivingBinCode = bin;
        ReceivedBy = staff;
        ReceivedAt = at;
        StatusId = ReturnStatuses.InInspection;
    }

    /// <summary>Decides one line, for a quantity from 1 to what the order had (RQC-4 to RQC-6, RQC-9).</summary>
    public ReturnCaseLine DecideLine(long lineId, short decisionId, int quantity, string? note, string staff, DateTimeOffset at)
    {
        RequireStaff(staff);
        if (StatusId != ReturnStatuses.InInspection)
        {
            throw new DomainException(StatusId == ReturnStatuses.Completed ? "This case is complete and cannot change." : "Receive the parcel before inspecting it.");
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new DomainException("That line is not on this case.");
        line.Decide(decisionId, quantity, note, staff, at);
        return line;
    }

    /// <summary>Freezes the case and announces the result of each line. Every line must be decided (RQC-7, RQC-8).</summary>
    public void Complete(string staff, DateTimeOffset at)
    {
        RequireStaff(staff);
        if (StatusId == ReturnStatuses.Completed)
        {
            throw new DomainException("This case is already complete.");
        }

        if (StatusId != ReturnStatuses.InInspection)
        {
            throw new DomainException("Receive the parcel before completing the case.");
        }

        var waiting = _lines.Count(l => !l.IsDecided);
        if (waiting > 0)
        {
            throw new DomainException($"{waiting} {(waiting == 1 ? "line is" : "lines are")} still waiting for a decision.");
        }

        StatusId = ReturnStatuses.Completed;
        CompletedBy = staff;
        CompletedAt = at;
        Raise(new ReturnQcCompletedEvent(
            OrderId,
            OrderNumber,
            [.. _lines.Select(l => new ReturnedLineResult(l.VariantId, l.Sku, ReturnDecisions.CodeOf(l.DecisionId!.Value), l.DecidedQuantity!.Value))]));
    }

    private static void RequireStaff(string staff)
    {
        if (string.IsNullOrWhiteSpace(staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }
    }
}
