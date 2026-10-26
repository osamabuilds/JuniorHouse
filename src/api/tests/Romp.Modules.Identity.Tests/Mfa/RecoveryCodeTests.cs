using System.Text;
using Romp.Modules.Identity.Domain.Mfa;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 4 (AC-6): ten recovery codes, each working once, stored only as a peppered hash.</summary>
public sealed class RecoveryCodeTests
{
    private static readonly byte[] Pepper = Encoding.ASCII.GetBytes("a-test-pepper-of-sufficient-length-0123");
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Issues_TenDistinctCodes_FromAnUnambiguousAlphabet()
    {
        var (rows, codes) = RecoveryCodes.Issue(7, Pepper, Now);

        Assert.Equal(10, codes.Count);
        Assert.Equal(10, rows.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{5}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{5}$", c));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void CodesWorkOnce_AndAreStoredHashed()
    {
        var (rows, codes) = RecoveryCodes.Issue(7, Pepper, Now);
        var code = codes[3];

        // Nothing stored contains the code, in any form.
        var stored = string.Join(' ', rows.Select(r => Convert.ToHexString(r.CodeHash)));
        Assert.DoesNotContain(code.Replace("-", string.Empty, StringComparison.Ordinal), stored, StringComparison.OrdinalIgnoreCase);
        Assert.All(rows, r => Assert.Equal(32, r.CodeHash.Length));

        var used = RecoveryCodes.TryUse(rows, 7, Pepper, code, Now.AddMinutes(5));
        Assert.NotNull(used);
        Assert.Equal(Now.AddMinutes(5), used.UsedAt);

        // A second use of the same code fails, and so does an unknown code; the other nine are unaffected.
        Assert.Null(RecoveryCodes.TryUse(rows, 7, Pepper, code, Now.AddMinutes(6)));
        Assert.Null(RecoveryCodes.TryUse(rows, 7, Pepper, "AAAAA-AAAAA", Now));
        Assert.NotNull(RecoveryCodes.TryUse(rows, 7, Pepper, codes[0], Now));
        Assert.Equal(8, RecoveryCodes.Remaining(rows));
    }

    [Theory]
    [Trait("Spec", "AC-6")]
    [InlineData("lower")]
    [InlineData("dashes")]
    [InlineData("spaces")]
    public void TheCodeIsAcceptedHoweverItIsTyped(string style)
    {
        var (rows, codes) = RecoveryCodes.Issue(7, Pepper, Now);
        var typed = style switch
        {
            "lower" => codes[0].ToLowerInvariant(),
            "dashes" => codes[0].Replace("-", string.Empty, StringComparison.Ordinal),
            _ => " " + codes[0].Replace("-", " ", StringComparison.Ordinal) + " ",
        };

        Assert.NotNull(RecoveryCodes.TryUse(rows, 7, Pepper, typed, Now));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void ACode_BelongsToOneUser_AndOnePepper()
    {
        var (rows, codes) = RecoveryCodes.Issue(7, Pepper, Now);

        Assert.Null(RecoveryCodes.TryUse(rows, 8, Pepper, codes[0], Now)); // another user's id
        Assert.Null(RecoveryCodes.TryUse(rows, 7, Encoding.ASCII.GetBytes("a-different-pepper-of-sufficient-len"), codes[0], Now));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Regenerate_InvalidatesOldCodes()
    {
        var (oldRows, oldCodes) = RecoveryCodes.Issue(7, Pepper, Now);
        var (newRows, newCodes) = RecoveryCodes.Issue(7, Pepper, Now.AddDays(1));

        // The new set replaces the old one (the caller deletes the old rows), so an old code finds no row.
        Assert.Null(RecoveryCodes.TryUse(newRows, 7, Pepper, oldCodes[0], Now));
        Assert.NotNull(RecoveryCodes.TryUse(newRows, 7, Pepper, newCodes[0], Now));
        Assert.Empty(newCodes.Intersect(oldCodes));
        Assert.Equal(10, oldRows.Count);
    }

    [Theory]
    [Trait("Spec", "AC-6")]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("ABCDE-FGHJK-LMNPQ")]
    [InlineData("0O1IL-0O1IL")] // the look-alike characters are not in the alphabet
    public void MalformedCodes_AreRejectedWithoutWork(string typed)
    {
        var (rows, _) = RecoveryCodes.Issue(7, Pepper, Now);

        Assert.Null(RecoveryCodes.TryUse(rows, 7, Pepper, typed, Now));
        Assert.False(RecoveryCodes.LooksLikeACode(typed));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void ASixDigitCode_IsNotMistakenForARecoveryCode_AndTheReverse()
    {
        Assert.True(RecoveryCodes.LooksLikeACode("ABCDE-FGHJK"));
        Assert.False(RecoveryCodes.LooksLikeACode("123456"));
        Assert.False(TotpVerifier.LooksLikeACode("ABCDE-FGHJK"));
        Assert.True(TotpVerifier.LooksLikeACode("123456"));
    }
}
