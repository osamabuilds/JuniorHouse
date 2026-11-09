namespace Romp.Modules.Identity.Application.Mfa;

/// <summary>A secret as stored: the id of the key that sealed it and the nonce, ciphertext and tag together. Never holds the plaintext.</summary>
public sealed record ProtectedSecret(string KeyId, byte[] Cipher);

/// <summary>
/// Seals the authenticator secret before it is stored and opens it when a code is checked (AC-7). <c>associatedData</c> ties a sealed
/// secret to its owner (for example <c>user:7</c>), so a row copied onto another account does not open.
/// </summary>
public interface ISecretProtector
{
    ProtectedSecret Protect(byte[] secret, string associatedData);

    /// <exception cref="System.Security.Cryptography.CryptographicException">The key is unknown or the data was changed.</exception>
    byte[] Unprotect(ProtectedSecret sealedSecret, string associatedData);

    /// <summary>True when the secret was sealed with a key that is no longer the current one (a rotation should re-seal it).</summary>
    bool NeedsRotation(ProtectedSecret sealedSecret);
}

/// <summary>The secret that makes recovery-code hashes useless to someone who only has the database (AC-6, AC-7).</summary>
public interface IRecoveryPepper
{
    byte[] Value { get; }
}
