using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Tests.Sessions;

public sealed class ThrottlePolicyTests
{
    private static readonly DateTimeOffset Last = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [Trait("Spec", "AC-6")]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(5, 120)]
    [InlineData(6, 240)]
    [InlineData(7, 480)]
    [InlineData(8, 900)]
    [InlineData(9, 900)]
    [InlineData(1000, 900)]
    public void Delays_DoubleFromTheThirdFailure_AndCap(int consecutiveFailures, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ThrottlePolicy.DelayAfter(consecutiveFailures));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Success_ResetsTheCount()
    {
        // Newest first: two failures after a success do not carry the earlier failures along.
        var outcomes = new[] { false, false, true, false, false, false, false };

        Assert.Equal(2, ThrottlePolicy.ConsecutiveFailures(outcomes));
        Assert.Equal(0, ThrottlePolicy.ConsecutiveFailures([true, false, false, false]));
        Assert.Equal(0, ThrottlePolicy.ConsecutiveFailures([]));
        Assert.Equal(3, ThrottlePolicy.ConsecutiveFailures([false, false, false]));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void RetryAfter_CountsDownFromTheLastAttempt_AndIsNullOnceElapsed()
    {
        // 3 consecutive failures = 30 s from the last attempt.
        Assert.Equal(TimeSpan.FromSeconds(30), ThrottlePolicy.RetryAfter(3, Last, Last));
        Assert.Equal(TimeSpan.FromSeconds(10), ThrottlePolicy.RetryAfter(3, Last, Last.AddSeconds(20)));
        Assert.Null(ThrottlePolicy.RetryAfter(3, Last, Last.AddSeconds(30)));
        Assert.Null(ThrottlePolicy.RetryAfter(2, Last, Last));
        Assert.Null(ThrottlePolicy.RetryAfter(0, Last, Last));
        // A clock that moved backwards never yields more than the full delay.
        Assert.Equal(TimeSpan.FromSeconds(30), ThrottlePolicy.RetryAfter(3, Last, Last.AddSeconds(-5)));
    }

    [Theory]
    [Trait("Spec", "AC-6")]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void GlobalPerNameCeiling_StartsAtTenFailuresInTheWindow(int failuresInWindow, bool reached)
    {
        Assert.Equal(reached, ThrottlePolicy.GlobalCeilingReached(failuresInWindow));
        Assert.Equal(TimeSpan.FromMinutes(15), ThrottlePolicy.GlobalWindow);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void GlobalCeiling_AppliesTheCappedDelay_WithoutLockingTheAccount()
    {
        Assert.Equal(TimeSpan.FromSeconds(900), ThrottlePolicy.GlobalDelay);
        // Delay is measured from the last failure, so it always ends: nothing is locked.
        Assert.Null(ThrottlePolicy.RetryAfter(ThrottlePolicy.GlobalDelay, Last, Last.AddSeconds(900)));
    }
}
