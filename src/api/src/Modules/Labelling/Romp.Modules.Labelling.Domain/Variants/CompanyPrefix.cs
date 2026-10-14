using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Domain.Variants;

/// <summary>
/// The GS1 company prefix every GTIN starts with (ADR 0009). A GTIN-13 has 12 payload digits, so the item reference gets
/// <c>12 - prefix length</c> digits: a longer prefix means LESS capacity (7 digits = 100,000 numbers, 8 = 10,000).
/// The restricted-circulation range 020-029 is the test prefix (D2) and is refused in production.
/// </summary>
public sealed record CompanyPrefix
{
    public const int PayloadLength = 12;
    public const int MinLength = 7;
    public const int MaxLength = 9;

    private CompanyPrefix(string value) => Value = value;

    public string Value { get; }

    public int Length => Value.Length;

    public int ItemReferenceWidth => PayloadLength - Length;

    /// <summary>How many item references exist under this prefix (0 .. Capacity - 1).</summary>
    public long Capacity => (long)Math.Pow(10, ItemReferenceWidth);

    /// <summary>True for the GS1 restricted-circulation range (020-029): never a real product barcode.</summary>
    public bool IsTestPrefix => Value.StartsWith("02", StringComparison.Ordinal);

    public static bool TryParse(string? value, out CompanyPrefix? prefix, out string? error)
    {
        prefix = null;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Any(c => c is < '0' or > '9'))
        {
            error = "The company prefix must contain digits only.";
            return false;
        }

        if (text.Length is < MinLength or > MaxLength)
        {
            error = $"The company prefix must be {MinLength} to {MaxLength} digits long.";
            return false;
        }

        prefix = new CompanyPrefix(text);
        error = null;
        return true;
    }

    public static CompanyPrefix Parse(string? value) =>
        TryParse(value, out var prefix, out var error) ? prefix! : throw new DomainException(error!);
}
