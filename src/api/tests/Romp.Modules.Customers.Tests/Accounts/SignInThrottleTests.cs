using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-9 reading A (Q-ACT-1, SEC-02): progressive delay and a CAPTCHA from the 3rd failure, and never a lock.</summary>
public sealed class SignInThrottleTests
{
    [Theory]
    [Trait("Spec", "ACT-9")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Evaluate_FewerThanThreeFailures_IsNotThrottled(int failures)
    {
        var decision = SignInThrottle.Evaluate(failures);

        Assert.Equal(TimeSpan.Zero, decision.Delay);
        Assert.False(decision.CaptchaRequired);
    }

    [Theory]
    [Trait("Spec", "ACT-9")]
    [InlineData(3, 2)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(6, 16)]
    [InlineData(7, 32)]
    public void Evaluate_FromTheThirdFailure_TheDelayDoublesAndTheCaptchaIsRequired(int failures, int seconds)
    {
        var decision = SignInThrottle.Evaluate(failures);

        Assert.Equal(TimeSpan.FromSeconds(seconds), decision.Delay);
        Assert.True(decision.CaptchaRequired);
    }

    [Theory]
    [Trait("Spec", "ACT-9")]
    [InlineData(8)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void Evaluate_TheDelayIsCapped_SoARealCustomerIsNeverShutOut(int failures)
    {
        var decision = SignInThrottle.Evaluate(failures);

        Assert.Equal(SignInThrottle.MaxDelay, decision.Delay);
        Assert.True(decision.CaptchaRequired);
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    public void Evaluate_NegativeCounts_AreTreatedAsNone()
    {
        Assert.Equal(TimeSpan.Zero, SignInThrottle.Evaluate(-4).Delay);
    }
}
