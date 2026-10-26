namespace Romp.Modules.Identity.Domain.Sessions;

/// <summary>
/// Progressive sign-in throttling (SEC-02, sessions AC-6). Pure: callers pass counts and times read from the attempt log.
/// Nothing here locks an account; every delay is measured from the last attempt, so it always ends.
/// </summary>
public static class ThrottlePolicy
{
    /// <summary>Consecutive failures that are free of delay; the third one starts the delay.</summary>
    public const int FreeFailures = 2;

    /// <summary>Failures for one name, from any address, inside <see cref="GlobalWindow"/> that trigger the global ceiling.</summary>
    public const int GlobalCeilingFailures = 10;

    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(900);
    public static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(15);

    /// <summary>The delay applied to a name once the global ceiling is reached (the cap, so rotating addresses gains nothing).</summary>
    public static readonly TimeSpan GlobalDelay = MaxDelay;

    /// <summary>Delay after N consecutive failures of one name+address pair: none up to 2, then 30 s doubling, capped at 900 s.</summary>
    public static TimeSpan DelayAfter(int consecutiveFailures)
    {
        if (consecutiveFailures <= FreeFailures)
        {
            return TimeSpan.Zero;
        }

        // Doubling saturates at the cap after 5 steps; clamping the exponent keeps the shift well-defined for any count.
        var doublings = Math.Min(consecutiveFailures - FreeFailures - 1, 10);
        var delay = TimeSpan.FromTicks(FirstDelay.Ticks << doublings);
        return delay > MaxDelay ? MaxDelay : delay;
    }

    /// <summary>Failures since the most recent success, given outcomes newest first (true = success). A success resets the count.</summary>
    public static int ConsecutiveFailures(IEnumerable<bool> succeededNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(succeededNewestFirst);

        var count = 0;
        foreach (var succeeded in succeededNewestFirst)
        {
            if (succeeded)
            {
                break;
            }

            count++;
        }

        return count;
    }

    /// <summary>Time left before the next attempt is allowed, or null when it may proceed (the Retry-After value).</summary>
    public static TimeSpan? RetryAfter(int consecutiveFailures, DateTimeOffset lastAttempt, DateTimeOffset now) =>
        RetryAfter(DelayAfter(consecutiveFailures), lastAttempt, now);

    public static TimeSpan? RetryAfter(TimeSpan delay, DateTimeOffset lastAttempt, DateTimeOffset now)
    {
        if (delay <= TimeSpan.Zero)
        {
            return null;
        }

        // A clock that moved backwards must not extend the wait beyond the full delay.
        var elapsed = now - lastAttempt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var remaining = delay - elapsed;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public static bool GlobalCeilingReached(int failuresInWindow) => failuresInWindow >= GlobalCeilingFailures;
}
