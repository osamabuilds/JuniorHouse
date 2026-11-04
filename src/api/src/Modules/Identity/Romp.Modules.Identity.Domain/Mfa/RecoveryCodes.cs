using System.Security.Cryptography;
using System.Text;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Identity.Domain.Mfa;

/// <summary>
/// IDNT.USR_RCVR_CODE: one recovery code of an account (AC-6). Only a peppered hash is stored, never the code. A code works once; using
/// it records the time.
/// </summary>
public sealed class RecoveryCode : Entity<long>, IAuditable
{
    private RecoveryCode()
    {
        CodeHash = [];
        InsrBy = string.Empty;
    }

    public long UserId { get; private set; }

    public byte[] CodeHash { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal static RecoveryCode Create(long userId, byte[] hash) => new() { UserId = userId, CodeHash = hash };

    internal void MarkUsed(DateTimeOffset now) => UsedAt = now;
}

/// <summary>
/// Makes and checks recovery codes (AC-6). A code is 10 characters from an alphabet without look-alikes (0/O, 1/I/L), shown as
/// <c>XXXXX-XXXXX</c>. The codes are random, so a fast hash with a secret pepper and the user id is enough to protect them at rest.
/// </summary>
public static class RecoveryCodes
{
    public const int Count = 10;
    private const int Length = 10;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>A new set. The caller replaces any earlier rows with these, which is what invalidates the old codes.</summary>
    public static (IReadOnlyList<RecoveryCode> Rows, IReadOnlyList<string> Codes) Issue(long userId, byte[] pepper, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pepper);

        var codes = new List<string>(Count);
        while (codes.Count < Count)
        {
            var raw = string.Create(Length, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
                }
            });
            var shown = $"{raw[..5]}-{raw[5..]}";
            if (!codes.Contains(shown))
            {
                codes.Add(shown);
            }
        }

        var rows = codes.Select(c => RecoveryCode.Create(userId, Hash(pepper, userId, Normalise(c)!))).ToList();
        return (rows, codes);
    }

    /// <summary>Marks the matching unused code as used and returns it, or null (wrong, already used, or not shaped like a code).</summary>
    public static RecoveryCode? TryUse(IEnumerable<RecoveryCode> rows, long userId, byte[] pepper, string? typed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(pepper);

        var normalised = Normalise(typed);
        if (normalised is null)
        {
            return null;
        }

        var hash = Hash(pepper, userId, normalised);
        RecoveryCode? match = null;
        foreach (var row in rows)
        {
            // Every row is compared so the time taken does not say which code matched.
            if (CryptographicOperations.FixedTimeEquals(row.CodeHash, hash) && row.UsedAt is null)
            {
                match = row;
            }
        }

        match?.MarkUsed(now);
        return match;
    }

    public static int Remaining(IEnumerable<RecoveryCode> rows) => rows.Count(r => r.UsedAt is null);

    public static bool LooksLikeACode(string? typed) => Normalise(typed) is not null;

    /// <summary>Upper-case, without dashes or spaces; null unless exactly ten characters of the alphabet remain.</summary>
    private static string? Normalise(string? typed)
    {
        if (typed is null || typed.Length > 32)
        {
            return null;
        }

        var cleaned = typed.Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return cleaned.Length == Length && cleaned.All(c => Alphabet.Contains(c, StringComparison.Ordinal)) ? cleaned : null;
    }

    private static byte[] Hash(byte[] pepper, long userId, string normalised)
    {
        var message = Encoding.ASCII.GetBytes($"{userId}:{normalised}");
        return HMACSHA256.HashData(pepper, message);
    }
}
