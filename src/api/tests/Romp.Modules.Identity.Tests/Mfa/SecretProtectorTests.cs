using System.Security.Cryptography;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Infrastructure.Mfa;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 3 (AC-7): the TOTP secret is encrypted at rest with a key that is not in the database, and the key can be rotated.</summary>
public sealed class SecretProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmSecretProtector Protector(string keyId, string key, Dictionary<string, string>? previous = null) =>
        new(Options.Create(new MfaOptions { KeyId = keyId, EncryptionKey = key, PreviousKeys = previous ?? [] }));

    [Fact]
    [Trait("Spec", "AC-7")]
    public void RoundTrips_AndRejectsTampering()
    {
        var protector = Protector("k1", NewKey());
        var secret = RandomNumberGenerator.GetBytes(20);

        var sealedSecret = protector.Protect(secret, "user:7");

        Assert.Equal("k1", sealedSecret.KeyId);
        Assert.Equal(secret, protector.Unprotect(sealedSecret, "user:7"));
        Assert.False(sealedSecret.Cipher.AsSpan().IndexOf(secret) >= 0); // the plaintext is not in the stored bytes

        // Flip one bit anywhere: nonce, ciphertext or tag. All are detected.
        for (var i = 0; i < sealedSecret.Cipher.Length; i++)
        {
            var tampered = sealedSecret.Cipher.ToArray();
            tampered[i] ^= 0x01;
            Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(new ProtectedSecret("k1", tampered), "user:7"));
        }
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void SameSecretTwice_GivesDifferentCiphertext()
    {
        var protector = Protector("k1", NewKey());
        var secret = RandomNumberGenerator.GetBytes(20);

        Assert.NotEqual(protector.Protect(secret, "user:7").Cipher, protector.Protect(secret, "user:7").Cipher);
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void ASecret_CannotBeMovedToAnotherUsersRow()
    {
        var protector = Protector("k1", NewKey());
        var sealedSecret = protector.Protect(RandomNumberGenerator.GetBytes(20), "user:7");

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(sealedSecret, "user:8"));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void OldKeyId_StillDecrypts()
    {
        var oldKey = NewKey();
        var secret = RandomNumberGenerator.GetBytes(20);
        var sealedByOld = Protector("k1", oldKey).Protect(secret, "user:7");

        // After rotation k2 is current and k1 is kept only to read what it wrote.
        var rotated = Protector("k2", NewKey(), new() { ["k1"] = oldKey });

        Assert.Equal(secret, rotated.Unprotect(sealedByOld, "user:7"));
        Assert.Equal("k2", rotated.Protect(secret, "user:7").KeyId);
        Assert.True(rotated.NeedsRotation(sealedByOld));
        Assert.False(rotated.NeedsRotation(rotated.Protect(secret, "user:7")));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void AnUnknownKeyId_IsRefused()
    {
        var sealedByOld = Protector("k1", NewKey()).Protect(RandomNumberGenerator.GetBytes(20), "user:7");
        var other = Protector("k2", NewKey());

        Assert.ThrowsAny<CryptographicException>(() => other.Unprotect(sealedByOld, "user:7"));
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ValidateOptionsResult Validate(string environment, MfaOptions options) =>
        new MfaOptionsValidator(new Env(environment)).Validate(null, options);

    [Fact]
    [Trait("Spec", "AC-7")]
    public void OutsideDevelopment_TheKeyAndThePepper_AreRequired_AndMustBeStrong()
    {
        var good = new MfaOptions { KeyId = "k1", EncryptionKey = NewKey(), RecoveryPepper = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        Assert.True(Validate("Production", good).Succeeded);

        Assert.True(Validate("Production", new MfaOptions { KeyId = "k1", RecoveryPepper = good.RecoveryPepper }).Failed);                       // no key
        Assert.True(Validate("Production", new MfaOptions { KeyId = "k1", EncryptionKey = good.EncryptionKey }).Failed);                        // no pepper
        Assert.True(Validate("Production", new MfaOptions { KeyId = "k1", EncryptionKey = Convert.ToBase64String(new byte[16]), RecoveryPepper = good.RecoveryPepper }).Failed); // 16-byte key
        Assert.True(Validate("Production", new MfaOptions { KeyId = "k1", EncryptionKey = "not base64!!", RecoveryPepper = good.RecoveryPepper }).Failed);
        Assert.True(Validate("Production", new MfaOptions { KeyId = "", EncryptionKey = good.EncryptionKey, RecoveryPepper = good.RecoveryPepper }).Failed);
        Assert.True(Validate("Production", new MfaOptions { KeyId = "k1", EncryptionKey = good.EncryptionKey, RecoveryPepper = Convert.ToBase64String(new byte[8]) }).Failed);
        Assert.True(Validate("Production", new MfaOptions
        {
            KeyId = "k1",
            EncryptionKey = good.EncryptionKey,
            RecoveryPepper = good.RecoveryPepper,
            PreviousKeys = new() { ["k1"] = NewKey() }, // an old key reusing the current id would make the current key ambiguous
        }).Failed);
    }

    [Theory]
    [Trait("Spec", "AC-7")]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void InDevelopment_AWellKnownDevelopmentKeyIsUsedWhenNoneIsConfigured(string environment)
    {
        Assert.True(Validate(environment, new MfaOptions()).Succeeded);
        var options = Options.Create(new MfaOptions());
        var protector = new AesGcmSecretProtector(options);

        Assert.Equal(new byte[] { 1, 2, 3 }, protector.Unprotect(protector.Protect([1, 2, 3], "user:1"), "user:1"));
    }
}
