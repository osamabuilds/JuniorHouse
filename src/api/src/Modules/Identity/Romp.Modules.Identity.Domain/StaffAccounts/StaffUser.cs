using Romp.BuildingBlocks.Domain;
using UserNameValue = Romp.Modules.Identity.Domain.StaffAccounts.UserName;

namespace Romp.Modules.Identity.Domain.StaffAccounts;

/// <summary>
/// What an account change needs to know about the Owner role, supplied by the caller who has read the other accounts. The aggregate
/// cannot see them itself; the same rule is re-checked atomically in the database (staff-accounts step 7).
/// </summary>
public readonly record struct OwnerGuard(short OwnerRoleId, bool OtherActiveOwnerExists);

/// <summary>IDNT.USR_ROLE_MAP: the account holds a role.</summary>
public sealed class StaffUserRole
{
    private StaffUserRole()
    {
    }

    internal StaffUserRole(short roleId)
    {
        RoleId = roleId;
    }

    public long UserId { get; private set; }

    public short RoleId { get; private set; }
}

/// <summary>IDNT.USR_PWD_HIST: a hash the account used before, kept so it cannot be reused at once.</summary>
public sealed class PasswordHistoryEntry : Entity<long>, IAuditable
{
    private PasswordHistoryEntry()
    {
        PasswordHash = string.Empty;
    }

    internal PasswordHistoryEntry(string passwordHash, DateTimeOffset retiredAt)
    {
        PasswordHash = passwordHash;
        RetiredAt = retiredAt;
    }

    public long UserId { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset RetiredAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// IDNT.USR_MAIN: a back-office account (staff-accounts). Accounts are never deleted, only deactivated. The user name never changes.
/// Passwords arrive already hashed; the aggregate never sees a plain password, so checking "is this the same password as before"
/// is done by a function the caller passes in.
/// </summary>
public sealed class StaffUser : AggregateRoot<long>, IAuditable
{
    public const int DisplayNameMaxLength = 100;
    public const int PasswordHistorySize = 3;
    public static readonly TimeSpan TemporaryPasswordLifetime = TimeSpan.FromHours(48);

    private readonly List<StaffUserRole> _roles = [];
    private readonly List<PasswordHistoryEntry> _history = [];

    private StaffUser()
    {
        UserName = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
        InsrBy = string.Empty;
    }

    private StaffUser(UserNameValue userName, string displayName, string passwordHash, IEnumerable<short> roleIds)
    {
        UserName = userName.Value;
        DisplayName = CleanDisplayName(displayName);
        PasswordHash = RequireHash(passwordHash);
        IsActive = true;
        MustChangePassword = true;
        InsrBy = string.Empty;
        _roles.AddRange(roleIds.Distinct().Select(id => new StaffUserRole(id)));
    }

    public string UserName { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    public bool MustChangePassword { get; private set; }

    /// <summary>Set while the current password is a generated one that has not been replaced; null once the person chose their own.</summary>
    public DateTimeOffset? TemporaryPasswordExpiresAt { get; private set; }

    public DateTimeOffset? TemporaryPasswordUsedAt { get; private set; }

    public bool HasTemporaryPassword => TemporaryPasswordExpiresAt is not null;

    /// <summary>The authenticator secret as sealed by the secret protector (mfa AC-7); null until enrolment is confirmed. Never the secret itself.</summary>
    public byte[]? MfaSecretCipher { get; private set; }

    /// <summary>Which key sealed <see cref="MfaSecretCipher"/>, so keys can be rotated.</summary>
    public string? MfaKeyId { get; private set; }

    public DateTimeOffset? MfaEnrolledAt { get; private set; }

    /// <summary>The newest time step whose code was accepted. A code for this step or an earlier one is a replay (mfa AC-4).</summary>
    public long MfaLastStep { get; private set; }

    public bool IsMfaEnrolled => MfaEnrolledAt is not null;

    public IReadOnlyCollection<short> RoleIds => _roles.Select(r => r.RoleId).ToArray();

    public IReadOnlyCollection<StaffUserRole> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<PasswordHistoryEntry> PasswordHistory => _history.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>An administrator creates the account with a generated password that works once, within 48 hours (AC-4).</summary>
    public static StaffUser CreateWithTemporaryPassword(
        UserNameValue userName, string displayName, string temporaryPasswordHash, IEnumerable<short> roleIds, DateTimeOffset now)
    {
        var user = new StaffUser(userName, displayName, temporaryPasswordHash, roleIds);
        user.TemporaryPasswordExpiresAt = now + TemporaryPasswordLifetime;
        return user;
    }

    /// <summary>The operator creates the Owner with a password they chose; it must still be changed at the first sign-in.</summary>
    public static StaffUser CreateWithPassword(UserNameValue userName, string displayName, string passwordHash, IEnumerable<short> roleIds) =>
        new(userName, displayName, passwordHash, roleIds);

    /// <summary>
    /// False when there is no temporary password to use, it has expired, or it was used already. A true result consumes it, so a
    /// second sign-in with the same value fails (AC-4, AC-6). The caller has already checked the password against the hash.
    /// </summary>
    public bool TryUseTemporaryPassword(DateTimeOffset now)
    {
        if (!IsActive || TemporaryPasswordExpiresAt is not { } expires || TemporaryPasswordUsedAt is not null || now > expires)
        {
            return false;
        }

        TemporaryPasswordUsedAt = now;
        return true;
    }

    /// <summary>An administrator resets the password: a new generated one, a new 48-hour window, the person must change it (AC-4, AC-6).</summary>
    public void IssueTemporaryPassword(string temporaryPasswordHash, DateTimeOffset now)
    {
        EnsureActive();
        PasswordHash = RequireHash(temporaryPasswordHash);
        MustChangePassword = true;
        TemporaryPasswordExpiresAt = now + TemporaryPasswordLifetime;
        TemporaryPasswordUsedAt = null;
    }

    /// <summary>
    /// The person chooses their own password. <paramref name="matchesStoredHash"/> says whether the new plain password matches a given
    /// stored hash; it must not equal the current password or any of the last three before it (AC-7).
    /// </summary>
    public void SetPassword(string newPasswordHash, Func<string, bool> matchesStoredHash)
    {
        ArgumentNullException.ThrowIfNull(matchesStoredHash);
        EnsureActive();
        RequireHash(newPasswordHash);

        if (matchesStoredHash(PasswordHash) || _history.Any(h => matchesStoredHash(h.PasswordHash)))
        {
            throw new DomainException("The new password was used recently. Choose a password you have not used lately.");
        }

        _history.Add(new PasswordHistoryEntry(PasswordHash, DateTimeOffset.UtcNow));
        while (_history.Count > PasswordHistorySize)
        {
            _history.RemoveAt(0);
        }

        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        TemporaryPasswordExpiresAt = null;
        TemporaryPasswordUsedAt = null;
    }

    /// <summary>
    /// Break-glass reset by the operator (AC-13): the operator chose the password, so there is no single-use window, but the person must
    /// still replace it at the next sign-in. Refused for a deactivated account.
    /// </summary>
    public void ResetPasswordByOperator(string passwordHash)
    {
        EnsureActive();
        PasswordHash = RequireHash(passwordHash);
        MustChangePassword = true;
        TemporaryPasswordExpiresAt = null;
        TemporaryPasswordUsedAt = null;
    }

    /// <summary>
    /// Confirmed enrolment (mfa AC-2): the sealed secret becomes active. <paramref name="firstStep"/> is the step of the code that
    /// confirmed it, so that code cannot be replayed to sign in.
    /// </summary>
    public void EnrolMfa(byte[] sealedSecret, string keyId, DateTimeOffset now, long firstStep)
    {
        ArgumentNullException.ThrowIfNull(sealedSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        if (IsMfaEnrolled)
        {
            throw new DomainException("Two-step verification is already set up for this account.");
        }

        MfaSecretCipher = sealedSecret;
        MfaKeyId = keyId;
        MfaEnrolledAt = now;
        MfaLastStep = firstStep;
    }

    /// <summary>Forgets the second factor (reset by an administrator or the operator): the person must enrol again (mfa AC-8, AC-9). Idempotent.</summary>
    public void ClearMfa()
    {
        MfaSecretCipher = null;
        MfaKeyId = null;
        MfaEnrolledAt = null;
        MfaLastStep = 0;
    }

    /// <summary>Records an accepted step; false (and no change) when it is not newer than the last one. The caller's save is the conditional write.</summary>
    public bool TryAcceptMfaStep(long step)
    {
        if (step <= MfaLastStep)
        {
            return false;
        }

        MfaLastStep = step;
        return true;
    }

    /// <summary>A key rotation re-seals the same secret under the current key (mfa AC-7).</summary>
    public void ReplaceSealedSecret(byte[] sealedSecret, string keyId)
    {
        ArgumentNullException.ThrowIfNull(sealedSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        MfaSecretCipher = sealedSecret;
        MfaKeyId = keyId;
    }

    /// <summary>Idempotent. Refused for the last active Owner (AC-9).</summary>
    public void Deactivate(OwnerGuard guard)
    {
        if (!IsActive)
        {
            return;
        }

        if (HoldsOwner(guard) && !guard.OtherActiveOwnerExists)
        {
            throw new DomainException("This is the last active Owner. Make someone else an Owner first.");
        }

        IsActive = false;
    }

    public void Reactivate() => IsActive = true;

    /// <summary>Replaces the account's roles. Dropping the Owner role from the last active Owner is refused (AC-9).</summary>
    public void SetRoles(IEnumerable<short> roleIds, OwnerGuard guard)
    {
        var wanted = roleIds.Distinct().ToList();
        if (IsActive && HoldsOwner(guard) && !wanted.Contains(guard.OwnerRoleId) && !guard.OtherActiveOwnerExists)
        {
            throw new DomainException("This is the last active Owner. Make someone else an Owner first.");
        }

        _roles.RemoveAll(r => !wanted.Contains(r.RoleId));
        foreach (var id in wanted.Where(id => _roles.TrueForAll(r => r.RoleId != id)))
        {
            _roles.Add(new StaffUserRole(id));
        }
    }

    private bool HoldsOwner(OwnerGuard guard) => _roles.Exists(r => r.RoleId == guard.OwnerRoleId);

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("The account is deactivated.");
        }
    }

    private static string RequireHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new DomainException("A password hash is required.");
        }

        return hash;
    }

    private static string CleanDisplayName(string displayName)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > DisplayNameMaxLength)
        {
            throw new DomainException($"The display name is required and at most {DisplayNameMaxLength} characters.");
        }

        return trimmed;
    }
}
