using System.Security.Cryptography;
using System.Text;

namespace Romp.BuildingBlocks.Security;

/// <summary>
/// A small, purpose-bound, tamper-proof token for links in email and on pages (order view, COD confirm). The token carries one subject
/// (an order number, say) and an optional expiry; the HMAC-SHA256 covers the purpose too, so a token made for one purpose never works for
/// another. Comparison is constant-time. The token proves the link was issued by us; it is not a session, and what it allows is decided
/// by the caller. The key comes from configuration or secrets, never from the repository (ADR 0024, SEC-25).
/// </summary>
public sealed class SignedLink
{
    public const int MinKeyBytes = 32;

    private const string Version = "v1";
    private readonly byte[] key;

    public SignedLink(ReadOnlySpan<byte> key)
    {
        if (key.Length < MinKeyBytes)
        {
            throw new ArgumentException($"A signing key needs at least {MinKeyBytes} bytes.", nameof(key));
        }

        this.key = key.ToArray();
    }

    /// <summary>Makes a token for <paramref name="subject"/> and <paramref name="purpose"/>; <paramref name="expiresAt"/> null means no expiry.</summary>
    public string Create(string purpose, string subject, DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var payload = $"{expiresAt?.ToUnixTimeSeconds() ?? 0}|{subject}";
        return $"{Encode(Encoding.UTF8.GetBytes(payload))}.{Encode(Sign(purpose, payload))}";
    }

    /// <summary>True when the token was made by <see cref="Create"/> for exactly this purpose, is untampered and has not expired.</summary>
    public bool TryValidate(string purpose, string? token, DateTimeOffset now, out string subject)
    {
        subject = string.Empty;
        if (string.IsNullOrEmpty(token) || token.Length > 1024 || string.IsNullOrEmpty(purpose))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2 || !TryDecode(parts[0], out var payloadBytes) || !TryDecode(parts[1], out var mac))
        {
            return false;
        }

        string payload;
        try
        {
            payload = new UTF8Encoding(false, true).GetString(payloadBytes);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(Sign(purpose, payload), mac))
        {
            return false;
        }

        var bar = payload.IndexOf('|', StringComparison.Ordinal);
        if (bar <= 0 || bar == payload.Length - 1 || !long.TryParse(payload.AsSpan(0, bar), out var expires))
        {
            return false;
        }

        if (expires != 0 && now.ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        subject = payload[(bar + 1)..];
        return true;
    }

    private byte[] Sign(string purpose, string payload) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{Version}|{purpose}|{payload}"));

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length == 0)
        {
            return false;
        }

        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        var buffer = new byte[padded.Length];
        if (!Convert.TryFromBase64String(padded, buffer, out var written))
        {
            return false;
        }

        bytes = buffer[..written];
        return true;
    }
}
