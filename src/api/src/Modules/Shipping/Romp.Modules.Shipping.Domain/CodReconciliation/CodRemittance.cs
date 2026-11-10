using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Domain.Bookings;

namespace Romp.Modules.Shipping.Domain.CodReconciliation;

/// <summary>One line of a courier statement (SHPG.RMTN_LINE) and what it was judged to be. A flagged line waits for a staff note (COD-12).</summary>
public sealed class CodRemittanceLine : Entity<long>, IAuditable
{
    public const int MaxNoteLength = 200;

    private CodRemittanceLine()
    {
        Waybill = string.Empty;
        InsrBy = string.Empty;
    }

    internal CodRemittanceLine(string waybill, decimal amount, long? shipmentId, short outcomeId)
    {
        Waybill = waybill;
        Amount = amount;
        ShipmentId = shipmentId;
        OutcomeId = outcomeId;
        InsrBy = string.Empty;
    }

    public long RemittanceId { get; private set; }

    public string Waybill { get; private set; }

    public decimal Amount { get; private set; }

    public long? ShipmentId { get; private set; }

    public short OutcomeId { get; private set; }

    public string? ResolutionNote { get; private set; }

    public string? ResolvedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsFlagged => OutcomeId != RemittanceOutcomes.Matched;

    public bool IsOpen => IsFlagged && ResolvedAt is null;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Closes a flagged line with a reason and the person (COD-12). A line is resolved once.</summary>
    public void Resolve(string note, string staff, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }

        if (!IsFlagged)
        {
            throw new DomainException("A matched line needs no resolution.");
        }

        if (ResolvedAt is not null)
        {
            throw new DomainException("This line was already resolved.");
        }

        var text = (note ?? string.Empty).Trim();
        if (text.Length is 0 or > MaxNoteLength)
        {
            throw new DomainException($"Say what happened in 1 to {MaxNoteLength} characters.");
        }

        ResolutionNote = text;
        ResolvedBy = staff;
        ResolvedAt = at;
    }
}

/// <summary>An unjudged line as a person entered it.</summary>
public sealed record CodStatementLine(string Waybill, decimal Amount);

/// <summary>
/// A courier's cash settlement statement (SHPG.RMTN_MAIN, FR-SC-17): a reference, a date, the total the courier says it paid and a line per waybill.
/// Recorded once with every line judged and never edited (COD-10). The stated total must equal the sum of the lines (COD-8).
/// </summary>
public sealed class CodRemittance : AggregateRoot<long>, IAuditable
{
    public const int MaxLines = 500;
    public const int MaxReferenceLength = 40;
    public const decimal MaxAmount = 9_999_999.99m;

    private readonly List<CodRemittanceLine> _lines = [];

    private CodRemittance()
    {
        Reference = string.Empty;
        InsrBy = string.Empty;
    }

    public short CourierId { get; private set; }

    public string Reference { get; private set; }

    public DateOnly RemittedOn { get; private set; }

    public decimal StatedTotal { get; private set; }

    public IReadOnlyList<CodRemittanceLine> Lines => _lines;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Checks a statement as typed, before any lookup: returns the cleaned reference and lines, or throws a plain message (COD-8, COD-16).</summary>
    public static (string Reference, IReadOnlyList<CodStatementLine> Lines) Validate(string? reference, decimal statedTotal, IReadOnlyList<CodStatementLine>? lines)
    {
        var cleanReference = (reference ?? string.Empty).Trim().ToUpperInvariant();
        if (cleanReference.Length is 0 or > MaxReferenceLength || !cleanReference.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '/' or '_'))
        {
            throw new DomainException($"The statement reference is 1 to {MaxReferenceLength} letters, digits, dashes, slashes or underscores.");
        }

        if (lines is null || lines.Count is 0 or > MaxLines)
        {
            throw new DomainException($"A statement has 1 to {MaxLines} lines.");
        }

        var clean = new List<CodStatementLine>(lines.Count);
        foreach (var line in lines)
        {
            var waybill = (line.Waybill ?? string.Empty).Trim().ToUpperInvariant();
            if (waybill.Length is 0 or > Shipment.MaxWaybillLength || !waybill.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                throw new DomainException("Every waybill is 1 to 40 letters, digits or dashes.");
            }

            if (line.Amount <= 0 || line.Amount > MaxAmount || decimal.Round(line.Amount, 2) != line.Amount)
            {
                throw new DomainException("Every amount is above zero, below 10,000,000, with at most two decimals.");
            }

            clean.Add(new CodStatementLine(waybill, line.Amount));
        }

        if (clean.Sum(l => l.Amount) != statedTotal)
        {
            throw new DomainException("The stated total does not equal the sum of the lines, so nothing was saved.");
        }

        return (cleanReference, clean);
    }

    public static CodRemittance Record(short courierId, string reference, DateOnly remittedOn, decimal statedTotal) =>
        new() { CourierId = courierId, Reference = reference, RemittedOn = remittedOn, StatedTotal = statedTotal };

    public CodRemittanceLine AddLine(string waybill, decimal amount, long? shipmentId, short outcomeId)
    {
        var line = new CodRemittanceLine(waybill, amount, shipmentId, outcomeId);
        _lines.Add(line);
        return line;
    }
}
