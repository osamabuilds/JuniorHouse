using System.Globalization;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Orders.Domain.Orders;

/// <summary>
/// The order number a customer reads out or types: <c>RMP-2026-00123-4</c> (ADR 0006 shape plus a Luhn check digit). The year and the
/// five-digit sequence come from the atomic counter; the last digit catches a mistyped number before any look-up. Guessing the next number
/// is harmless because look-up needs a second factor (Q-ORD-3).
/// </summary>
public static class OrderNumbers
{
    public const string Prefix = "RMP";
    public const int MaxLength = 20;

    public static string Format(int year, int sequence)
    {
        if (year is < 2000 or > 2999)
        {
            throw new DomainException("An order number needs a four-digit year.");
        }

        if (sequence is < 1 or > 99_999_999)
        {
            throw new DomainException("An order sequence is between 1 and 99,999,999.");
        }

        var body = $"{year}{sequence.ToString("00000", CultureInfo.InvariantCulture)}";
        return $"{Prefix}-{year}-{sequence.ToString("00000", CultureInfo.InvariantCulture)}-{CheckDigit(body)}";
    }

    /// <summary>True when the text has the shape and the check digit is right. Case and surrounding spaces are tolerated by <see cref="Normalise"/>.</summary>
    public static bool IsValid(string? text)
    {
        var parts = (text ?? string.Empty).Split('-');
        if (parts.Length != 4 || parts[0] != Prefix || parts[1].Length != 4 || parts[2].Length < 5 || parts[3].Length != 1)
        {
            return false;
        }

        if (!parts.Skip(1).All(p => p.All(char.IsAsciiDigit)))
        {
            return false;
        }

        return CheckDigit(parts[1] + parts[2]) == parts[3][0];
    }

    /// <summary>The text a customer typed, upper-cased and trimmed, or null when it is not a valid order number.</summary>
    public static string? Normalise(string? text)
    {
        var cleaned = (text ?? string.Empty).Trim().ToUpperInvariant();
        return IsValid(cleaned) ? cleaned : null;
    }

    // Luhn over the digits of "YYYYNNNNN": the standard double-every-second-digit-from-the-right algorithm, returning the digit that makes the sum a multiple of ten.
    private static char CheckDigit(string digits)
    {
        var sum = 0;
        var doubleIt = true;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }
}
