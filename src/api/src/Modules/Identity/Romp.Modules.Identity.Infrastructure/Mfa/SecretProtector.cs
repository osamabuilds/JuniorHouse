using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Mfa;

namespace Romp.Modules.Identity.Infrastructure.Mfa;

/// <summary><c>Identity:Mfa:*</c> - the keys behind the stored authenticator secrets and the recovery-code pepper (mfa AC-7, AC-6).</summary>
public sealed class MfaOptions
{
    public const string Section = "Identity:Mfa";

    public const int KeyBytes = 32;
    public const int MinPepperBytes = 16;

    /// <summary>Names the current key inside each sealed secret, so a key can be replaced without a flag day.</summary>
    public string? KeyId { get; set; }

    /// <summary>Base64 of 32 random bytes: a deployment secret, never in source or the database.</summary>
    public string? EncryptionKey { get; set; }

    /// <summary>Earlier keys by id, kept only to open secrets they sealed until those are re-sealed.</summary>
    public Dictionary<string, string> PreviousKeys { get; set; } = [];

    /// <summary>Base64 of at least 16 random bytes: mixed into every recovery-code hash.</summary>
    public string? RecoveryPepper { get; set; }
}

public sealed class MfaOptionsValidator(IHostEnvironment environment) : IValidateOptions<MfaOptions>
{
    public ValidateOptionsResult Validate(string? name, MfaOptions options)
    {
        if (RompAuthorizationExtensions.IsDevelopmentLike(environment) && string.IsNullOrEmpty(options.EncryptionKey) && string.IsNullOrEmpty(options.RecoveryPepper))
        {
            return ValidateOptionsResult.Success; // a well-known development key stands in; see DevelopmentMfaKeys
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.KeyId))
        {
            errors.Add($"{MfaOptions.Section}:KeyId is required.");
        }

        CheckKey(options.EncryptionKey, MfaOptions.KeyBytes, exact: true, $"{MfaOptions.Section}:EncryptionKey", errors);
        CheckKey(options.RecoveryPepper, MfaOptions.MinPepperBytes, exact: false, $"{MfaOptions.Section}:RecoveryPepper", errors);
        foreach (var (id, key) in options.PreviousKeys)
        {
            CheckKey(key, MfaOptions.KeyBytes, exact: true, $"{MfaOptions.Section}:PreviousKeys:{id}", errors);
            if (id == options.KeyId)
            {
                errors.Add($"{MfaOptions.Section}:PreviousKeys cannot reuse the current key id.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void CheckKey(string? value, int bytes, bool exact, string name, List<string> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add($"{name} is required outside Development and Testing.");
            return;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written))
        {
            errors.Add($"{name} must be base64.");
        }
        else if (exact ? written != bytes : written < bytes)
        {
            errors.Add($"{name} must hold {(exact ? "exactly" : "at least")} {bytes} bytes.");
        }
    }
}

/// <summary>
/// The keys used when none is configured in Development/Testing, so a developer's database keeps working across restarts. They are
/// public, which is the point of refusing to start without real ones anywhere else (<see cref="MfaOptionsValidator"/>).
/// </summary>
internal static class DevelopmentMfaKeys
{
    public const string KeyId = "dev";
    public static readonly byte[] Key = SHA256.HashData(Encoding.ASCII.GetBytes("romp-development-mfa-encryption-key"));
    public static readonly byte[] Pepper = SHA256.HashData(Encoding.ASCII.GetBytes("romp-development-recovery-pepper"));
}

/// <summary>
/// AES-256-GCM. The stored bytes are nonce (12) + ciphertext + tag (16); the key id travels beside them. The owner's id is bound in as
/// associated data. A fresh random nonce is made for every sealing, so the same secret never seals to the same bytes.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly string _currentId;
    private readonly byte[] _currentKey;
    private readonly Dictionary<string, byte[]> _keys = [];

    public AesGcmSecretProtector(IOptions<MfaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        if (string.IsNullOrEmpty(value.EncryptionKey))
        {
            _currentId = DevelopmentMfaKeys.KeyId;
            _currentKey = DevelopmentMfaKeys.Key;
        }
        else
        {
            _currentId = value.KeyId ?? throw new InvalidOperationException("The MFA key id is missing.");
            _currentKey = Convert.FromBase64String(value.EncryptionKey);
        }

        _keys[_currentId] = _currentKey;
        foreach (var (id, key) in value.PreviousKeys)
        {
            _keys[id] = Convert.FromBase64String(key);
        }
    }

    public ProtectedSecret Protect(byte[] secret, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var data = new byte[NonceBytes + secret.Length + TagBytes];
        var nonce = data.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_currentKey, TagBytes);
        aes.Encrypt(nonce, secret, data.AsSpan(NonceBytes, secret.Length), data.AsSpan(NonceBytes + secret.Length, TagBytes), Encoding.UTF8.GetBytes(associatedData));
        return new ProtectedSecret(_currentId, data);
    }

    public byte[] Unprotect(ProtectedSecret sealedSecret, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(sealedSecret);

        if (!_keys.TryGetValue(sealedSecret.KeyId, out var key))
        {
            throw new CryptographicException("The secret was sealed with an unknown key.");
        }

        var data = sealedSecret.Cipher;
        if (data.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("The sealed secret is too short.");
        }

        var length = data.Length - NonceBytes - TagBytes;
        var plain = new byte[length];
        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(data.AsSpan(0, NonceBytes), data.AsSpan(NonceBytes, length), data.AsSpan(NonceBytes + length, TagBytes), plain, Encoding.UTF8.GetBytes(associatedData));
        return plain;
    }

    public bool NeedsRotation(ProtectedSecret sealedSecret)
    {
        ArgumentNullException.ThrowIfNull(sealedSecret);

        return sealedSecret.KeyId != _currentId;
    }
}

/// <summary>The recovery-code pepper from configuration (or the development one).</summary>
public sealed class ConfiguredRecoveryPepper(IOptions<MfaOptions> options) : IRecoveryPepper
{
    public byte[] Value { get; } = string.IsNullOrEmpty(options.Value.RecoveryPepper) ? DevelopmentMfaKeys.Pepper : Convert.FromBase64String(options.Value.RecoveryPepper);
}
