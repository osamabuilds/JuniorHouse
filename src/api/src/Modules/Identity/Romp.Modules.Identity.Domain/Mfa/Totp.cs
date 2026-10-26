using System.Globalization;
using System.Security.Cryptography;

namespace Romp.Modules.Identity.Domain.Mfa;

/// <summary>RFC 4648 base32 without padding, the form authenticator apps expect for a manually typed secret.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var result = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    /// <summary>Decodes, ignoring case, spaces and trailing padding; any other character is a <see cref="FormatException"/>.</summary>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var output = new List<byte>(text.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var raw in text)
        {
            if (raw is ' ' or '=')
            {
                continue;
            }

            var value = Alphabet.IndexOf(char.ToUpperInvariant(raw), StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException("Not a base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}

/// <summary>The time-based one-time password of RFC 6238 with the settings every authenticator app supports: SHA-1, 6 digits, 30 seconds.</summary>
public static class Totp
{
    public const int Digits = 6;
    public const int StepSeconds = 30;

    public static long StepOf(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>The six-digit code for one time step (RFC 4226 dynamic truncation of HMAC-SHA1).</summary>
    public static string Code(byte[] secret, long step)
    {
        ArgumentNullException.ThrowIfNull(secret);

        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
#pragma warning disable CA5350 // HMAC-SHA1 is what RFC 6238 and every authenticator app use; its weakness is in plain hashing, not in HMAC
        HMACSHA1.HashData(secret, counter, hash);
#pragma warning restore CA5350

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Checks a typed code (AC-3, AC-4). The code of the current step and of one step either side are accepted, to allow for a slightly
/// wrong clock, but a step that is not newer than the last accepted one never is: a code works once (replay protection).
/// </summary>
public static class TotpVerifier
{
    /// <summary>Exactly six ASCII digits, after spaces are removed.</summary>
    public static bool LooksLikeACode(string? typed) => Normalise(typed) is not null;

    /// <returns>The accepted step, which the caller must store as the new "last accepted step"; null when the code is not valid.</returns>
    public static long? Verify(byte[] secret, string? typed, DateTimeOffset now, long lastAcceptedStep)
    {
        var code = Normalise(typed);
        if (code is null)
        {
            return null;
        }

        var current = Totp.StepOf(now);
        long? accepted = null;
        for (var step = current - 1; step <= current + 1; step++)
        {
            // Every candidate is compared, whatever the earlier ones did, so the time taken does not say which step matched.
            var matches = CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(Totp.Code(secret, step)), System.Text.Encoding.ASCII.GetBytes(code));
            if (matches && step > lastAcceptedStep)
            {
                accepted = step;
            }
        }

        return accepted;
    }

    private static string? Normalise(string? typed)
    {
        if (typed is null || typed.Length > 16)
        {
            return null;
        }

        var digits = typed.Replace(" ", string.Empty, StringComparison.Ordinal);
        return digits.Length == Totp.Digits && digits.All(c => c is >= '0' and <= '9') ? digits : null;
    }
}

/// <summary>The shared secret of an authenticator (AC-2): 160 bits from the system's secure random source, and how it is shown.</summary>
public static class TotpSecret
{
    public const int Bytes = 20;

    public static byte[] Generate() => RandomNumberGenerator.GetBytes(Bytes);

    /// <summary>The <c>otpauth://</c> address an authenticator app reads from a QR code. Label and issuer are percent-encoded, so no text can add a parameter.</summary>
    public static string OtpAuthUri(string issuer, string accountName, byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var encodedIssuer = Uri.EscapeDataString(issuer);
        var label = $"{encodedIssuer}:{Uri.EscapeDataString(accountName)}";
        return $"otpauth://totp/{label}?secret={Base32.Encode(secret)}&issuer={encodedIssuer}&algorithm=SHA1&digits={Totp.Digits}&period={Totp.StepSeconds}";
    }
}
