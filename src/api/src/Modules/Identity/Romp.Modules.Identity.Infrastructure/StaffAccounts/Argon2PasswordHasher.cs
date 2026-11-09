using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.StaffAccounts;

/// <summary><c>Identity:Argon2:*</c> - Argon2id cost. The defaults aim at roughly 250 ms on the VPS (ADR 0015).</summary>
public sealed class Argon2Options
{
    public const string Section = "Identity:Argon2";

    public int MemoryKiB { get; set; } = 65536;

    public int Iterations { get; set; } = 3;

    public int Parallelism { get; set; } = 1;
}

public sealed class Argon2OptionsValidator(IHostEnvironment environment) : IValidateOptions<Argon2Options>
{
    internal const int MinMemoryKiB = 19456;
    internal const int MinIterations = 2;

    public ValidateOptionsResult Validate(string? name, Argon2Options options)
    {
        if (options.MemoryKiB < 8 || options.Iterations < 1 || options.Parallelism < 1)
        {
            return ValidateOptionsResult.Fail("Identity:Argon2 memory, iterations and parallelism must be positive (memory at least 8 KiB).");
        }

        if (!RompAuthorizationExtensions.IsDevelopmentLike(environment)
            && (options.MemoryKiB < MinMemoryKiB || options.Iterations < MinIterations))
        {
            return ValidateOptionsResult.Fail(
                $"Identity:Argon2 is too weak outside Development and Testing: memory must be at least {MinMemoryKiB} KiB and iterations at least {MinIterations}.");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>Argon2id with a random 16-byte salt, stored as <c>$argon2id$v=19$m=..,t=..,p=..$salt$hash</c> (base64 without padding).</summary>
public sealed class Argon2PasswordHasher(IOptions<Argon2Options> options) : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Version = 19;

    private static readonly SecretString EmptyPlaceholder = new("\0");

    public string Hash(SecretString password)
    {
        var settings = options.Value;
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Compute(password, salt, settings.MemoryKiB, settings.Iterations, settings.Parallelism, HashBytes);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"$argon2id$v={Version}$m={settings.MemoryKiB},t={settings.Iterations},p={settings.Parallelism}${Encode(salt)}${Encode(hash)}");
    }

    public bool Verify(SecretString password, string storedHash)
    {
        if (!TryParse(storedHash, out var parsed))
        {
            return false;
        }

        // Argon2 refuses an empty password. A sign-in form can send one, so it does the same work as any other wrong password
        // (same time, no exception) and never matches; the policy makes an empty password impossible to have set.
        var isEmpty = password.Length == 0;
        var actual = Compute(isEmpty ? EmptyPlaceholder : password, parsed.Salt, parsed.Memory, parsed.Iterations, parsed.Parallelism, parsed.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(actual, parsed.Hash) && !isEmpty;
    }

    public bool NeedsRehash(string storedHash)
    {
        if (!TryParse(storedHash, out var parsed))
        {
            return true;
        }

        var settings = options.Value;
        return parsed.Memory < settings.MemoryKiB || parsed.Iterations < settings.Iterations || parsed.Parallelism != settings.Parallelism;
    }

    private static byte[] Compute(SecretString password, byte[] salt, int memory, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password.Reveal()))
        {
            Salt = salt,
            MemorySize = memory,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(length);
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length == 0)
        {
            return false;
        }

        var padded = text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParse(string stored, out ParsedHash parsed)
    {
        parsed = default;
        var parts = stored.Split('$');
        if (parts.Length != 6 || parts[0].Length != 0 || parts[1] != "argon2id" || parts[2] != $"v={Version}")
        {
            return false;
        }

        var costs = parts[3].Split(',');
        if (costs.Length != 3
            || !TryNumber(costs[0], "m=", out var memory)
            || !TryNumber(costs[1], "t=", out var iterations)
            || !TryNumber(costs[2], "p=", out var parallelism)
            || !TryDecode(parts[4], out var salt)
            || !TryDecode(parts[5], out var hash)
            || salt.Length < 8
            || hash.Length < 16)
        {
            return false;
        }

        parsed = new ParsedHash(memory, iterations, parallelism, salt, hash);
        return true;
    }

    private static bool TryNumber(string text, string prefix, out int value)
    {
        value = 0;
        return text.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value is > 0 and <= 262_144;
    }

    private readonly record struct ParsedHash(int Memory, int Iterations, int Parallelism, byte[] Salt, byte[] Hash);
}
