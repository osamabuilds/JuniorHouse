using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Orders.Domain.Orders;

/// <summary>What a guest order look-up must prove or wait for, given how many recent attempts came from the same address.</summary>
public sealed record LookupDecision(bool CaptchaRequired, bool RateLimited);

/// <summary>
/// The guest order look-up throttle (SEC-06, Q-OLK-3). Nothing is asked for the first two failures; from the third failure the next
/// attempt needs the CAPTCHA, and an address is cut off after twenty attempts in an hour. Same shape as the customer sign-in throttle,
/// but this module's own code (modules never share internals).
/// </summary>
public static class LookupThrottle
{
    /// <summary>Failures allowed before the CAPTCHA is asked for.</summary>
    public const int FreeFailures = 2;

    /// <summary>Attempts, failed or not, one address may make inside <see cref="Window"/>.</summary>
    public const int MaxAttemptsPerWindow = 20;

    /// <summary>How far back attempts are counted.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <param name="recentFailures">Failed attempts from this address since its last success, inside the window.</param>
    /// <param name="attemptsInWindow">All attempts from this address inside the window.</param>
    public static LookupDecision Evaluate(int recentFailures, int attemptsInWindow) =>
        new(CaptchaRequired: recentFailures > FreeFailures, RateLimited: attemptsInWindow >= MaxAttemptsPerWindow);
}

/// <summary>
/// ORDR.LOOK_ATMP: one row per guest look-up attempt. Only a hash of the client address is kept (never the order number or the mobile
/// that was typed), so the table can drive the throttle without becoming a list of probed numbers. Append-only; purged daily.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: this table is itself the look-up record; rows are append-only.")]
public sealed class LookupAttempt : Entity<long>, IAuditable
{
    public const int AddressHashLength = 64;

    private LookupAttempt()
    {
        AddressHash = string.Empty;
        InsrBy = string.Empty;
    }

    public string AddressHash { get; private set; }

    public bool Failed { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static LookupAttempt Record(string addressHash, bool failed, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressHash);
        if (addressHash.Length != AddressHashLength)
        {
            throw new ArgumentException($"The address hash must be {AddressHashLength} characters.", nameof(addressHash));
        }

        return new LookupAttempt { AddressHash = addressHash, Failed = failed, AttemptedAt = now };
    }
}
