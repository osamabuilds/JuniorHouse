using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

public sealed class PasswordPolicyTests
{
    private const string UserName = "amna.khan";

    private static readonly PasswordPolicy Policy = new(new EmbeddedCommonPasswords());

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Rejects_ShortLongNameEqualAndCommon()
    {
        Assert.Contains(PasswordPolicyFailure.TooShort, Policy.Evaluate(new string('a', 11), UserName).Failures);
        Assert.Contains(PasswordPolicyFailure.TooLong, Policy.Evaluate(new string('a', 129), UserName).Failures);
        Assert.Contains(PasswordPolicyFailure.EqualsUserName, Policy.Evaluate("amna.khan", "amna.khan").Failures);
        Assert.Contains(PasswordPolicyFailure.EqualsUserName, Policy.Evaluate("AMNA.KHAN.", "amna.khan.").Failures);
        Assert.Contains(PasswordPolicyFailure.Common, Policy.Evaluate("password1234", UserName).Failures);
        Assert.Contains(PasswordPolicyFailure.Common, Policy.Evaluate("PassWord1234", UserName).Failures);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Accepts_LongUncommonPasswordsAtTheLimits()
    {
        Assert.True(Policy.Evaluate("tangerine-ladder-47", UserName).IsAcceptable);
        Assert.True(Policy.Evaluate("kV9#mQ2$pL7z", UserName).IsAcceptable);
        Assert.True(Policy.Evaluate(string.Concat(Enumerable.Repeat("kV9#mQ2$", 16)), UserName).IsAcceptable);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Reasons_AreReportedTogether_AndAreHumanReadable()
    {
        var result = Policy.Evaluate("amna.khan", "amna.khan");

        Assert.False(result.IsAcceptable);
        Assert.Contains(PasswordPolicyFailure.TooShort, result.Failures);
        Assert.Contains(PasswordPolicyFailure.EqualsUserName, result.Failures);
        Assert.All(result.Failures, f => Assert.False(string.IsNullOrWhiteSpace(PasswordPolicyMessages.For(f))));
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void EmbeddedList_IsLoaded_AndIgnoresCase()
    {
        var list = new EmbeddedCommonPasswords();

        Assert.True(list.Contains("qwertyuiop12"));
        Assert.True(list.Contains("QWERTYUIOP12"));
        Assert.False(list.Contains("tangerine-ladder-47"));
    }
}
