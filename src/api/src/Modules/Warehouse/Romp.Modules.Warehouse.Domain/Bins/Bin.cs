using System.Text.RegularExpressions;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain.Bins;

/// <summary>
/// A shelf location in the simple zone-aisle-shelf scheme (BRD §5.13). The code is built from the three parts, upper-cased, and is
/// what is printed on the shelf and scanned (<c>A-03-2</c>). A bin is never deleted; it is deactivated, and only when it is empty
/// (BIN-4). Maps to WHSE.BIN_MAIN.
/// </summary>
public sealed class Bin : AggregateRoot<long>, IAuditable
{
    // Zone 1-3 letters/digits, aisle 1-3 digits, shelf 1-2 letters/digits (Q-BIN-1 default). Only these characters can ever reach a
    // label, a log line or a SQL parameter.
    private static readonly Regex ZonePattern = new("^[A-Z0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AislePattern = new("^[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ShelfPattern = new("^[A-Z0-9]{1,2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Bin()
    {
        BinCode = string.Empty;
        Zone = string.Empty;
        Aisle = string.Empty;
        Shelf = string.Empty;
    }

    public Bin(string zone, string aisle, string shelf)
    {
        Zone = Normalise(zone);
        Aisle = Normalise(aisle);
        Shelf = Normalise(shelf);

        var problem = Check(Zone, Aisle, Shelf);
        if (problem is not null)
        {
            throw new DomainException(problem);
        }

        BinCode = $"{Zone}-{Aisle}-{Shelf}";
        IsActive = true;
    }

    /// <summary>
    /// The first problem with the three parts (BIN-3), or null when they are acceptable. The parts are trimmed and upper-cased the
    /// same way the constructor does, so a validator and the entity can never disagree.
    /// </summary>
    public static string? Check(string? zone, string? aisle, string? shelf)
    {
        if (!ZonePattern.IsMatch(Normalise(zone)))
        {
            return "The zone must be 1 to 3 capital letters or digits.";
        }

        if (!AislePattern.IsMatch(Normalise(aisle)))
        {
            return "The aisle must be 1 to 3 digits.";
        }

        return ShelfPattern.IsMatch(Normalise(shelf)) ? null : "The shelf must be 1 or 2 capital letters or digits.";
    }

    public string BinCode { get; private set; }

    public string Zone { get; private set; }

    public string Aisle { get; private set; }

    public string Shelf { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>When a cycle count last covered this bin (written by cycle-counts through <c>IBinCountMarker</c>).</summary>
    public DateTimeOffset? LastCountedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>
    /// Activates or deactivates the bin. Deactivating a bin that holds stock is refused (BIN-4); setting the state it already
    /// has changes nothing. <paramref name="unitsOnHand"/> comes from the stock ledger through <c>IBinStockQuery</c>.
    /// </summary>
    public void SetActive(bool active, int unitsOnHand)
    {
        if (active == IsActive)
        {
            return;
        }

        if (!active && unitsOnHand > 0)
        {
            throw new DomainException($"Bin {BinCode} holds stock ({unitsOnHand} units) and cannot be deactivated.");
        }

        IsActive = active;
    }

    public void MarkCounted(DateTimeOffset countedAt) => LastCountedAt = countedAt;

    private static string Normalise(string? part) => (part ?? string.Empty).Trim().ToUpperInvariant();
}
