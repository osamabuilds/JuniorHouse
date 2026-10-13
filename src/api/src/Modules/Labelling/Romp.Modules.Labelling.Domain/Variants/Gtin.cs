using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Domain.Variants;

/// <summary>A valid GTIN-13 (EAN-13): 13 digits whose last digit is the GS1 modulo-10 check digit (FR-SC-06).</summary>
public sealed record Gtin
{
    private Gtin(string value) => Value = value;

    public string Value { get; }

    /// <summary>The first 12 digits: what a printer's EAN-13 command takes (it adds the check digit itself).</summary>
    public string Payload => Value[..12];

    /// <summary>Builds the GTIN for an item reference under a company prefix (ADR 0009). The reference must fit the prefix's capacity.</summary>
    public static Gtin Create(CompanyPrefix prefix, long itemReference)
    {
        if (itemReference < 0 || itemReference >= prefix.Capacity)
        {
            throw new DomainException(
                $"Item reference {itemReference} does not fit under prefix {prefix.Value}: capacity is {prefix.Capacity:N0} numbers.");
        }

        var payload = prefix.Value + itemReference.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(prefix.ItemReferenceWidth, '0');
        return new Gtin(payload + CheckDigit(payload));
    }

    /// <summary>Strict parsing (VAR-14): exactly 13 digits and a correct check digit, or it is not a GTIN.</summary>
    public static bool TryParse(string? value, out Gtin? gtin)
    {
        gtin = null;
        if (value is null || value.Length != 13 || value.Any(c => c is < '0' or > '9'))
        {
            return false;
        }

        if (CheckDigit(value[..12]) != value[12])
        {
            return false;
        }

        gtin = new Gtin(value);
        return true;
    }

    public static Gtin Parse(string? value) =>
        TryParse(value, out var gtin) ? gtin! : throw new DomainException("That is not a valid GTIN-13 (13 digits with a correct check digit).");

    /// <summary>GS1 modulo-10 over the 12 payload digits: from the right, weights 3, 1, 3, 1, …</summary>
    public static char CheckDigit(string payload12)
    {
        if (payload12.Length != 12 || payload12.Any(c => c is < '0' or > '9'))
        {
            throw new ArgumentException("The payload must be 12 digits.", nameof(payload12));
        }

        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = payload12[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    public override string ToString() => Value;
}
