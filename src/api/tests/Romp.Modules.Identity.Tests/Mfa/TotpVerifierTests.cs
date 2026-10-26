using System.Text;
using Romp.Modules.Identity.Domain.Mfa;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 2 (AC-3, AC-4): the six-digit time-based code of RFC 6238, with one step of clock drift and replay protection.</summary>
public sealed class TotpVerifierTests
{
    /// <summary>The RFC 6238 test secret for SHA-1: the ASCII bytes of "12345678901234567890".</summary>
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    private static DateTimeOffset At(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void MatchesRfcVectors(long unixSeconds, string expected)
    {
        // RFC 6238 appendix B lists 8-digit values; their last six digits are the six-digit code (the truncation is the same).
        Assert.Equal(expected, Totp.Code(RfcSecret, Totp.StepOf(At(unixSeconds))));
        Assert.NotNull(TotpVerifier.Verify(RfcSecret, expected, At(unixSeconds), lastAcceptedStep: 0));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void AllowsOneStepOfDrift()
    {
        var now = At(1111111111);
        var step = Totp.StepOf(now);

        Assert.Equal(step - 1, TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step - 1), now, 0));
        Assert.Equal(step, TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step), now, 0));
        Assert.Equal(step + 1, TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step + 1), now, 0));
        Assert.Null(TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step - 2), now, 0));
        Assert.Null(TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step + 2), now, 0));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void ReusedCode_IsRejected()
    {
        var now = At(1111111111);
        var step = Totp.StepOf(now);
        var code = Totp.Code(RfcSecret, step);

        var accepted = TotpVerifier.Verify(RfcSecret, code, now, lastAcceptedStep: 0);
        Assert.Equal(step, accepted);

        // The same code again, and an older code inside the drift window, both fail once that step has been used.
        Assert.Null(TotpVerifier.Verify(RfcSecret, code, now, accepted!.Value));
        Assert.Null(TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step - 1), now, accepted.Value));
        // A newer step is still fine.
        Assert.Equal(step + 1, TotpVerifier.Verify(RfcSecret, Totp.Code(RfcSecret, step + 1), now, accepted.Value));
    }

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("١٢٣٤٥٦")] // Arabic-Indic digits are not accepted
    [InlineData("287082\n")]
    public void MalformedCodes_AreRejected(string code)
    {
        Assert.Null(TotpVerifier.Verify(RfcSecret, code, At(59), 0));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void ASpaceInTheMiddle_FromHowAuthenticatorAppsShowCodes_IsAccepted()
    {
        Assert.NotNull(TotpVerifier.Verify(RfcSecret, "287 082", At(59), 0));
        Assert.NotNull(TotpVerifier.Verify(RfcSecret, " 287082 ", At(59), 0));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void AWrongCode_IsRejected_ForAnySecret()
    {
        Assert.Null(TotpVerifier.Verify(RfcSecret, "000000", At(59), 0));
        Assert.Null(TotpVerifier.Verify(Encoding.ASCII.GetBytes("another-secret-value!"), "287082", At(59), 0));
    }
}

public sealed class Base32Tests
{
    [Theory]
    [Trait("Spec", "AC-2")]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void EncodesTheRfc4648Vectors_WithoutPadding(string text, string expected)
    {
        Assert.Equal(expected, Base32.Encode(Encoding.ASCII.GetBytes(text)));
        Assert.Equal(text, Encoding.ASCII.GetString(Base32.Decode(expected)));
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void RoundTrips_A160BitSecret()
    {
        var secret = System.Security.Cryptography.RandomNumberGenerator.GetBytes(TotpSecret.Bytes);

        Assert.Equal(secret, Base32.Decode(Base32.Encode(secret)));
        Assert.Equal(32, Base32.Encode(secret).Length); // 160 bits is exactly 32 base32 characters
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Decode_IsCaseAndSpaceTolerant_AndRejectsJunk()
    {
        Assert.Equal(Encoding.ASCII.GetBytes("foobar"), Base32.Decode("mzxw 6ytb oi"));
        Assert.Throws<FormatException>(() => Base32.Decode("MZXW6YTB0I")); // 0 is not in the alphabet
    }
}

public sealed class TotpSecretTests
{
    [Fact]
    [Trait("Spec", "AC-2")]
    public void ANewSecret_Is160Bits_AndDifferentEachTime()
    {
        var first = TotpSecret.Generate();
        var second = TotpSecret.Generate();

        Assert.Equal(20, first.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void TheAuthenticatorUri_CarriesTheSecret_WithTheLabelEncoded()
    {
        var secret = Encoding.ASCII.GetBytes("12345678901234567890");

        var uri = TotpSecret.OtpAuthUri("Romp Admin", "amna.khan", secret);

        Assert.Equal(
            "otpauth://totp/Romp%20Admin:amna.khan?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=Romp%20Admin&algorithm=SHA1&digits=6&period=30",
            uri);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void ALabelWithSpecialCharacters_CannotInjectParameters()
    {
        var uri = TotpSecret.OtpAuthUri("Romp & Co?x=1", "a b&secret=EVIL", new byte[20]);

        Assert.DoesNotContain("secret=EVIL", uri, StringComparison.Ordinal);
        Assert.Equal(1, uri.Split("secret=").Length - 1);
    }
}
