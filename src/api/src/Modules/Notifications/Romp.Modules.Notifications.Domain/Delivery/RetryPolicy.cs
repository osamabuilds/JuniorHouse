using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>
/// When to try a delivery again (DLV-7). The schedule is configuration: one delay per attempt, so its length is the number of attempts
/// before a delivery is dead-lettered. At least <see cref="MinimumAttempts"/> attempts are required (NFR-FT-03) and jitter spreads retries so
/// a provider outage does not end in one burst. Pure: the caller supplies the random sample.
/// </summary>
public sealed class RetryPolicy
{
    public const int MinimumAttempts = 5;

    private readonly TimeSpan[] _delays;

    public RetryPolicy(IEnumerable<TimeSpan> delays, double jitter)
    {
        _delays = delays.ToArray();
        if (_delays.Length < MinimumAttempts)
        {
            throw new DomainException($"A retry schedule needs at least {MinimumAttempts} attempts.");
        }

        if (_delays.Any(d => d <= TimeSpan.Zero))
        {
            throw new DomainException("Every retry delay must be positive.");
        }

        if (jitter is < 0 or >= 1)
        {
            throw new DomainException("Jitter must be at least 0 and below 1.");
        }

        Jitter = jitter;
    }

    /// <summary>The documented default (Q-DLV-2): 1 min, 5 min, 30 min, 2 h, 6 h, 12 h with 20 % jitter.</summary>
    public static RetryPolicy Default { get; } = new(
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(12)],
        0.2);

    public double Jitter { get; }

    /// <summary>How many attempts a delivery gets before it is dead-lettered.</summary>
    public int MaxAttempts => _delays.Length;

    public TimeSpan FirstDelay => _delays[0];

    /// <summary>
    /// The wait after attempt <paramref name="attempt"/> (1-based) failed. <paramref name="sample"/> is a random number from -1 to 1;
    /// the base delay is stretched or shrunk by up to <see cref="Jitter"/>.
    /// </summary>
    public TimeSpan DelayAfter(int attempt, double sample)
    {
        if (attempt < 1 || attempt >= MaxAttempts)
        {
            throw new DomainException("No retry follows this attempt.");
        }

        var factor = 1 + (Jitter * Math.Clamp(sample, -1, 1));
        return _delays[attempt - 1] * factor;
    }
}
