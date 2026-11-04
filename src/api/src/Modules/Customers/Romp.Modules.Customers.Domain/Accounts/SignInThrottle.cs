namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>What a sign-in attempt must wait for and prove, given how many recent attempts failed.</summary>
public sealed record ThrottleDecision(TimeSpan Delay, bool CaptchaRequired);

/// <summary>
/// Progressive throttling instead of a lock (ACT-9 reading A, SEC-02, Q-ACT-1): nothing happens for the first two failures; from the third the
/// next attempt is delayed (2 s, doubling, capped) and needs the CAPTCHA. The cap keeps the delay short enough that a real customer who
/// is being attacked can still get in, and the account is never locked.
/// </summary>
public static class SignInThrottle
{
    public const int FreeFailures = 2;
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    public static ThrottleDecision Evaluate(int recentFailures)
    {
        if (recentFailures <= FreeFailures)
        {
            return new ThrottleDecision(TimeSpan.Zero, CaptchaRequired: false);
        }

        // 2 s for the 3rd failure, doubling each time; 2^5 = 32 s, the next doubling passes the cap.
        var doublings = recentFailures - FreeFailures - 1;
        var delay = doublings >= 5 ? MaxDelay : TimeSpan.FromTicks(FirstDelay.Ticks << doublings);
        return new ThrottleDecision(delay > MaxDelay ? MaxDelay : delay, CaptchaRequired: true);
    }
}
