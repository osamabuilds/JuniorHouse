using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain;

/// <summary>
/// GTIN-13 / EAN-13 validation (GS1 modulo-10), so a scanned number is checked before anything is looked up (PUT-6). Warehouse keeps
/// its own copy of this rule rather than reaching into Labelling's domain (ADR 0002).
/// </summary>
public static class Gtin13
{
    public static bool IsValid(string? gtin)
    {
        if (gtin is null || gtin.Length != 13 || !gtin.All(char.IsAsciiDigit))
        {
            return false;
        }

        return CheckDigit(gtin[..12]) == gtin[12] - '0';
    }

    /// <summary>Appends the GS1 check digit to a 12-digit payload.</summary>
    public static string WithCheckDigit(string payload12)
    {
        if (payload12.Length != 12 || !payload12.All(char.IsAsciiDigit))
        {
            throw new DomainException("A GTIN payload is exactly 12 digits.");
        }

        return payload12 + CheckDigit(payload12);
    }

    private static int CheckDigit(string payload12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = payload12[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (10 - (sum % 10)) % 10;
    }
}
