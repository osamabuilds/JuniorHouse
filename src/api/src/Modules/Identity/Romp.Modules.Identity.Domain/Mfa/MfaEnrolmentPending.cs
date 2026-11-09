using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Identity.Domain.Mfa;

/// <summary>
/// IDNT.MFA_ENRL_PEND: an authenticator secret that has been shown but not yet confirmed with a valid code (mfa plan). It is kept apart
/// from the account so a half-finished enrolment never leaves an active secret behind; one per account, valid for 15 minutes.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: an unconfirmed enrolment that lives for minutes and holds an encrypted secret.")]
public sealed class MfaEnrolmentPending : Entity<long>, IAuditable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private MfaEnrolmentPending()
    {
        SecretCipher = [];
        KeyId = string.Empty;
        InsrBy = string.Empty;
    }

    public long UserId { get; private set; }

    /// <summary>The secret as sealed by the secret protector; never the plain secret.</summary>
    public byte[] SecretCipher { get; private set; }

    public string KeyId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static MfaEnrolmentPending Start(long userId, byte[] sealedSecret, string keyId, DateTimeOffset now) =>
        new() { UserId = userId, SecretCipher = sealedSecret, KeyId = keyId, ExpiresAt = now + Lifetime };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
