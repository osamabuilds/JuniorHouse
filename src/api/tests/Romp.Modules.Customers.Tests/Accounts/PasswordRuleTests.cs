using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-2 and Q-ACT-3: 8..128 characters, a capital, a small letter and a digit, no spaces; special characters allowed.</summary>
public sealed class PasswordRuleTests
{
    [Theory]
    [Trait("Spec", "ACT-2")]
    [InlineData("Abcdefg1")]
    [InlineData("Romp2026kids")]
    [InlineData("Abcdef1!@#$%^&*()")]
    [InlineData("Pässwörd9")]
    public void Check_PasswordsThatMeetTheRule_AreValid(string password)
    {
        Assert.True(PasswordRule.IsValid(password));
        Assert.All(PasswordRule.Check(password), c => Assert.True(c.Met));
    }

    [Theory]
    [Trait("Spec", "ACT-2")]
    [InlineData("Abcde1", PasswordRule.Length)]
    [InlineData("abcdefg1", PasswordRule.Capital)]
    [InlineData("ABCDEFG1", PasswordRule.Small)]
    [InlineData("Abcdefgh", PasswordRule.Digit)]
    [InlineData("Abcd efg1", PasswordRule.NoSpaces)]
    [InlineData(" Abcdefg1", PasswordRule.NoSpaces)]
    public void Check_ABrokenRule_IsReportedByName_AndTheOthersStayMet(string password, string broken)
    {
        var checks = PasswordRule.Check(password);

        Assert.False(PasswordRule.IsValid(password));
        Assert.Equal([broken], checks.Where(c => !c.Met).Select(c => c.Code));
    }

    [Fact]
    [Trait("Spec", "ACT-2")]
    public void Check_NothingTyped_ListsEveryRuleAsNotMetExceptNoSpaces()
    {
        var checks = PasswordRule.Check(null);

        Assert.Equal(
            [PasswordRule.Length, PasswordRule.Capital, PasswordRule.Small, PasswordRule.Digit],
            checks.Where(c => !c.Met).Select(c => c.Code));
    }

    [Fact]
    [Trait("Spec", "ACT-2")]
    public void Check_TheListIsTheSameFixedOrder_SoTheBrowserCanShowIt()
    {
        var codes = PasswordRule.Check("x").Select(c => c.Code);

        Assert.Equal([PasswordRule.Length, PasswordRule.Capital, PasswordRule.Small, PasswordRule.Digit, PasswordRule.NoSpaces], codes);
        Assert.All(PasswordRule.Check("x"), c => Assert.False(string.IsNullOrWhiteSpace(c.Message)));
    }

    [Theory]
    [Trait("Spec", "ACT-2")]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Check_TheLongestAcceptedPasswordIs128Characters(int length, bool valid)
    {
        var password = "Aa1" + new string('x', length - 3);

        Assert.Equal(valid, PasswordRule.IsValid(password));
    }
}
