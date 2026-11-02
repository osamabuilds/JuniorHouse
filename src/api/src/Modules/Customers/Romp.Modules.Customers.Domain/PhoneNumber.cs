namespace Romp.Modules.Customers.Domain;

/// <summary>
/// One stored form for a Pakistani phone number: <c>+92</c> followed by ten digits (a mobile such as 3001234567, or a landline's area code
/// and number). The customer may type spaces, dashes, brackets, a leading 0, 92 or 0092. Anything else is refused (ADD-5; the Q-ACT-7
/// default, changeable by editing this one class).
/// </summary>
public static class PhoneNumber
{
    public const int MaxLength = 20;

    private const int NationalLength = 10;

    public static string Normalise(string? input) =>
        TryNormalise(input, out var normalised)
            ? normalised
            : throw new ArgumentException("Enter a Pakistani phone number such as 0300 1234567.", nameof(input));

    /// <summary>
    /// Sign-up takes a mobile only (ACT-29, Q-ACT-7): <c>+92</c> then <c>3</c> and nine more digits. A landline is a valid address phone but not a
    /// sign-up number, because the SMS code could not reach it.
    /// </summary>
    public static bool TryNormaliseMobile(string? input, out string normalised)
    {
        if (TryNormalise(input, out normalised) && normalised.StartsWith("+923", StringComparison.Ordinal))
        {
            return true;
        }

        normalised = string.Empty;
        return false;
    }

    public static bool TryNormalise(string? input, out string normalised)
    {
        normalised = string.Empty;
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > MaxLength)
        {
            return false;
        }

        var hadPlus = text[0] == '+';
        var digits = new List<char>(text.Length);
        foreach (var c in hadPlus ? text[1..] : text)
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Add(c);
            }
            else if (c is not (' ' or '-' or '(' or ')'))
            {
                return false;
            }
        }

        var national = new string(digits.ToArray());
        if (national.StartsWith("0092", StringComparison.Ordinal) && !hadPlus)
        {
            national = national[4..];
        }
        else if (national.StartsWith("92", StringComparison.Ordinal) && (hadPlus || national.Length == NationalLength + 2))
        {
            national = national[2..];
        }
        else if (national.StartsWith('0') && !hadPlus)
        {
            national = national[1..];
        }
        else if (hadPlus)
        {
            return false;
        }

        // Area codes and mobile prefixes start 2-9; a leading 0 or 1 is not a Pakistani number.
        if (national.Length != NationalLength || national[0] is '0' or '1')
        {
            return false;
        }

        normalised = "+92" + national;
        return true;
    }
}
