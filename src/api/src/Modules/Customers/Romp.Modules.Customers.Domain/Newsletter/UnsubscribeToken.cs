using System.Security.Cryptography;
using System.Text;

namespace Romp.Modules.Customers.Domain.Newsletter;

/// <summary>
/// The signed value in an unsubscribe link (NLS-3): <c>base64url(subscriptionId).base64url(HMAC-SHA256(key, subscriptionId))</c>. It names
/// one record by its id, carries no email address, cannot be forged without the key and cannot be edited to point at another record.
/// </summary>
public static class UnsubscribeToken
{
    public const int MaxLength = 200;
    public const int MinKeyLength = 16;

    public static string Create(long subscriptionId, string key)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subscriptionId);

        var id = Encoding.ASCII.GetBytes(subscriptionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return $"{Base64Url.Encode(id)}.{Base64Url.Encode(Sign(id, key))}";
    }

    /// <summary>Checks the token strictly and returns the subscription id it names, or null for anything malformed, oversized or not signed with this key.</summary>
    public static long? Verify(string? token, string key)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxLength)
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        var id = Base64Url.Decode(parts[0]);
        var signature = Base64Url.Decode(parts[1]);
        if (id is null || signature is null || id.Length is 0 or > 19)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(Sign(id, key), signature))
        {
            return null;
        }

        var text = Encoding.ASCII.GetString(id);
        if (!text.All(char.IsAsciiDigit) || !long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return null;
        }

        return value;
    }

    private static byte[] Sign(byte[] id, string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < MinKeyLength)
        {
            throw new InvalidOperationException("The link signing key is missing or too short.");
        }

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), id);
    }

    private static class Base64Url
    {
        public static string Encode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[]? Decode(string text)
        {
            if (text.Length == 0)
            {
                return null;
            }

            var padded = text.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            try
            {
                return Convert.FromBase64String(padded);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
