using System.Globalization;

namespace Romp.Modules.Customers.Domain.Newsletter;

/// <summary>
/// Email handling for the consent record. The check is deliberately short (RFC-lite): one at sign, text on both sides, a dot in the
/// domain, no spaces or control characters, at most 254 characters (NLS-1). Real proof that the address exists is the welcome email.
/// </summary>
public static class NewsletterEmail
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
