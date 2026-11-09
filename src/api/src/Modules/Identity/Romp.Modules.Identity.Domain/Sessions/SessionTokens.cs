using System.Security.Cryptography;
using System.Text;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Domain.Sessions;

/// <summary>
/// Session and refresh tokens (sessions AC-1): 32 random bytes as base64url, stored only as a SHA-256 hash. The values are
/// high-entropy, so a fast hash is enough; lookup is by hash through a unique index and any in-memory comparison is constant-time.
/// A generated token is a <see cref="SecretString"/>, so logging it by mistake prints a mask.
/// </summary>
public static class SessionTokens
{
    public const int ByteLength = 32;
    public const int TextLength = 43;

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
