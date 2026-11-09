using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>
/// A password or token value. <see cref="ToString"/> prints a mask and JSON serialisation throws, so logging or returning one by mistake
/// cannot leak it (SEC-25, ACT-15). Only <see cref="Reveal"/> gives the value.
/// </summary>
[JsonConverter(typeof(SecretStringJsonConverter))]
public sealed class SecretString
{
    private readonly string _value;

    public SecretString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    public int Length => _value.Length;

    public string Reveal() => _value;

    public override string ToString() => "********";
}

internal sealed class SecretStringJsonConverter : JsonConverter<SecretString>
{
    public override SecretString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("A SecretString is never read from JSON.");

    public override void Write(Utf8JsonWriter writer, SecretString value, JsonSerializerOptions options) =>
        throw new NotSupportedException("A SecretString is never written to JSON.");
}

/// <summary>
/// Session, refresh and one-time link tokens (ACT-11, ACT-15): 32 random bytes as base64url, stored only as a SHA-256 hash. The values are
/// high-entropy, so a fast hash is enough; lookup is by hash through a unique index and any in-memory comparison is constant-time.
/// </summary>
public static class CustomerTokens
{
    public const int ByteLength = 32;

    /// <summary>The longest text that can possibly be a token; anything longer is refused before hashing.</summary>
    private const int MaxPresentedLength = 128;

    public static SecretString Generate() => new(Base64Url(RandomNumberGenerator.GetBytes(ByteLength)));

    public static byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    /// <summary>True when <paramref name="presented"/> hashes to <paramref name="storedHash"/>; never throws on junk input.</summary>
    public static bool Matches(string? presented, byte[] storedHash)
    {
        ArgumentNullException.ThrowIfNull(storedHash);
        if (string.IsNullOrEmpty(presented) || presented.Length > MaxPresentedLength)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Hash(presented), storedHash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
