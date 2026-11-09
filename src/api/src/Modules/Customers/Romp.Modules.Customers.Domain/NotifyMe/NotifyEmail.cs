using System.Globalization;

namespace Romp.Modules.Customers.Domain.NotifyMe;

/// <summary>
/// Email handling for back-in-stock requests (NTM-2, NTM-6). The check is deliberately short: one at sign, text on both sides, a dot in the
/// domain, no spaces or control characters, at most 254 characters. Notify Me keeps its own copy so it depends on no other feature.
/// </summary>
public static class NotifyEmail
{
    public const int MaxLength = 254;

    /// <summary>The trimmed, lower-case address, or null when it does not look like an email address.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var email = raw.Trim().ToLower(CultureInfo.InvariantCulture);
        if (email.Length > MaxLength)
        {
            return null;
        }

        foreach (var c in email)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || c is ',' or ';' or '<' or '>' or '"' or '(' or ')' or '\\')
            {
                return null;
            }
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != email.LastIndexOf('@'))
        {
            return null;
        }

        var domain = email[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        if (dot <= 0 || dot == domain.Length - 1 || domain.StartsWith('.') || domain.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return email;
    }
}
