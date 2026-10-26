using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Application.Sessions;

/// <summary>
/// Reads the attempt log to say how long a sign-in step must wait (sessions AC-6). Shared by the password step and the MFA step, so a
/// run of wrong codes slows the next password attempt and the reverse (mfa AC-5).
/// </summary>
public sealed class SignInThrottle(IIdentityDbContext dbContext)
{
    /// <summary>How many recent attempts of a name+address pair are read; the delay reaches its cap well before this many failures.</summary>
    private const int PairLookback = 10;

    /// <summary>
    /// How long this attempt must still wait, or null. Two reads, both on the attempt index: the newest attempts of the name+address
    /// pair (progressive delay) and the failures for the name from anywhere in the last 15 minutes (global ceiling). Throttled
    /// attempts are ignored by both, so hammering during a delay does not lengthen it, and unknown names are treated like known ones.
    /// </summary>
    public async Task<TimeSpan?> WaitAsync(string typed, string clientIp, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pair = await dbContext.LoginAttempts.AsNoTracking()
            .Where(a => a.AttemptName == typed && a.ClientIp == clientIp && LoginOutcomes.Counted.Contains(a.Outcome))
            .OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id)
            .Select(a => new { a.Outcome, a.AttemptedAt })
            .Take(PairLookback)
            .ToListAsync(cancellationToken);

        TimeSpan? pairWait = null;
        if (pair.Count > 0)
        {
            var failures = ThrottlePolicy.ConsecutiveFailures(pair.Select(a => LoginOutcomes.IsSuccess(a.Outcome)));
            pairWait = ThrottlePolicy.RetryAfter(failures, pair[0].AttemptedAt, now);
        }

        var since = now - ThrottlePolicy.GlobalWindow;
        var global = await dbContext.LoginAttempts.AsNoTracking()
            .Where(a => a.AttemptName == typed && a.AttemptedAt >= since && LoginOutcomes.Failures.Contains(a.Outcome))
            .GroupBy(a => a.AttemptName)
            .Select(g => new { Count = g.Count(), Last = g.Max(a => a.AttemptedAt) })
            .FirstOrDefaultAsync(cancellationToken);

        TimeSpan? globalWait = global is not null && ThrottlePolicy.GlobalCeilingReached(global.Count)
            ? ThrottlePolicy.RetryAfter(ThrottlePolicy.GlobalDelay, global.Last, now)
            : null;

        return (pairWait, globalWait) switch
        {
            (null, null) => null,
            (null, { } g) => g,
            ({ } p, null) => p,
            ({ } p, { } g) => p > g ? p : g,
        };
    }
}
