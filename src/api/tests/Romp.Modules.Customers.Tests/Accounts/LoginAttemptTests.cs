using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-10: one row per attempt with the email as typed (truncated, control characters removed), the IP, the outcome and the time.</summary>
public sealed class LoginAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "ACT-10")]
    public void Record_KeepsTheEmailAsTyped_ButRemovesControlCharactersAndTruncates()
    {
        var attempt = LoginAttempt.Record("  Parent@Example.com\r\n\t" + new string('x', 400), "203.0.113.9", LoginOutcome.BadCredentials, Now);

        Assert.StartsWith("Parent@Example.com", attempt.AttemptedEmail);
        Assert.DoesNotContain('\r', attempt.AttemptedEmail);
        Assert.DoesNotContain('\n', attempt.AttemptedEmail);
        Assert.DoesNotContain('\t', attempt.AttemptedEmail);
        Assert.Equal(LoginAttempt.EmailMaxLength, attempt.AttemptedEmail.Length);
        Assert.Equal(Now, attempt.AttemptedAt);
        Assert.Equal(LoginOutcome.BadCredentials, attempt.Outcome);
    }

    [Fact]
    [Trait("Spec", "ACT-10")]
    public void Record_NothingTyped_StoresAnEmptyEmail_AndALongIpIsTruncated()
    {
        var attempt = LoginAttempt.Record(null, new string('9', 100), LoginOutcome.Throttled, Now, customerId: 7);

        Assert.Equal(string.Empty, attempt.AttemptedEmail);
        Assert.Equal(CustomerSession.ClientIpMaxLength, attempt.ClientIp.Length);
        Assert.Equal(7, attempt.CustomerId);
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    public void OnlyWrongPasswordsAndFailedCaptchasCountAsFailures()
    {
        Assert.Equal([LoginOutcome.BadCredentials, LoginOutcome.CaptchaFailed], LoginOutcomes.Failures);
        Assert.True(LoginOutcomes.IsSuccess(LoginOutcome.Ok));
        Assert.DoesNotContain(LoginOutcome.Throttled, LoginOutcomes.Counted);
        Assert.DoesNotContain(LoginOutcome.RefreshReuse, LoginOutcomes.Counted);
    }
}
