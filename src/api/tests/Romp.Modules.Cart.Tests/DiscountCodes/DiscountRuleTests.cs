using Romp.BuildingBlocks.Domain;
using Romp.Modules.Cart.Domain.DiscountCodes;

namespace Romp.Modules.Cart.Tests.DiscountCodes;

public sealed class DiscountRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static DiscountCode Code(
        string code = "WELCOME10", DiscountKind kind = DiscountKind.Percentage, decimal value = 10m, decimal? min = null, DateTimeOffset? expires = null,
        int? cap = null, int? perCustomer = 1, bool includeSale = false, short[]? categories = null) =>
        DiscountCode.Create(code, kind, value, min, expires, cap, perCustomer, includeSale, categories ?? [], Now.AddDays(-1));

    private static DiscountLine Line(decimal price, int quantity = 1, short category = 3, bool sale = false) => new(price, quantity, category, sale);

    [Fact]
    [Trait("Spec", "DSC-1")]
    public void APercentageCode_TakesThatShareOfTheEligibleSubtotal()
    {
        var result = DiscountRule.Evaluate(Code(value: 15m), [Line(1000m, 2), Line(500m)], Now, 0, 0);

        Assert.True(result.IsAccepted);
        Assert.Equal(375m, result.Amount);
    }

    [Fact]
    [Trait("Spec", "DSC-1")]
    public void AFixedCode_TakesThatAmount_ButNeverMoreThanTheEligibleSubtotal()
    {
        var code = Code(kind: DiscountKind.Fixed, value: 700m);

        Assert.Equal(700m, DiscountRule.Evaluate(code, [Line(1000m)], Now, 0, 0).Amount);
        Assert.Equal(400m, DiscountRule.Evaluate(code, [Line(400m)], Now, 0, 0).Amount);
    }

    [Fact]
    [Trait("Spec", "DSC-2")]
    public void AnExpiredCode_IsRefusedAsExpired_AndOneAtItsExactExpiryToo()
    {
        var code = Code(expires: Now.AddMinutes(1));

        Assert.True(DiscountRule.Evaluate(code, [Line(1000m)], Now, 0, 0).IsAccepted);
        Assert.Equal(DiscountRejection.Expired, DiscountRule.Evaluate(code, [Line(1000m)], Now.AddMinutes(1), 0, 0).Rejection);
        Assert.Equal(DiscountRejection.Expired, DiscountRule.Evaluate(code, [Line(1000m)], Now.AddDays(3), 0, 0).Rejection);
    }

    [Fact]
    [Trait("Spec", "DSC-3")]
    public void ACustomerWhoHasUsedTheirAllowance_IsToldSo_AndAGuestIsNotCheckedYet()
    {
        var code = Code(perCustomer: 1);

        Assert.Equal(DiscountRejection.AlreadyUsed, DiscountRule.Evaluate(code, [Line(1000m)], Now, 0, 1).Rejection);
        Assert.True(DiscountRule.Evaluate(code, [Line(1000m)], Now, 0, 0).IsAccepted);
        Assert.True(DiscountRule.Evaluate(code, [Line(1000m)], Now, 0, null).IsAccepted);
    }

    [Fact]
    [Trait("Spec", "DSC-5")]
    public void ACodeWhoseUsageCapIsReached_IsNoLongerAvailable()
    {
        var code = Code(cap: 5, perCustomer: null);

        Assert.True(DiscountRule.Evaluate(code, [Line(1000m)], Now, 4, null).IsAccepted);
        Assert.Equal(DiscountRejection.Exhausted, DiscountRule.Evaluate(code, [Line(1000m)], Now, 5, null).Rejection);
    }

    [Fact]
    [Trait("Spec", "DSC-6")]
    public void ABasketBelowTheMinimumSpend_IsToldTheMinimum()
    {
        var code = Code(min: 2000m);

        var below = DiscountRule.Evaluate(code, [Line(1999.99m)], Now, 0, 0);

        Assert.Equal(DiscountRejection.BelowMinimum, below.Rejection);
        Assert.Equal(2000m, below.MinimumSpend);
        Assert.True(DiscountRule.Evaluate(code, [Line(2000m)], Now, 0, 0).IsAccepted);
    }

    [Fact]
    [Trait("Spec", "DSC-7")]
    public void ACategoryScopedCode_DiscountsOnlyLinesInThoseCategories()
    {
        var code = Code(value: 10m, categories: [3, 5]);

        var result = DiscountRule.Evaluate(code, [Line(1000m, category: 3), Line(2000m, category: 9), Line(500m, category: 5)], Now, 0, 0);

        Assert.Equal(150m, result.Amount);
    }

    [Fact]
    [Trait("Spec", "DSC-7")]
    public void WhenNoLineIsInScope_TheReasonSaysSo()
    {
        var result = DiscountRule.Evaluate(Code(categories: [3]), [Line(1000m, category: 9)], Now, 0, 0);

        Assert.Equal(DiscountRejection.NoEligibleLines, result.Rejection);
    }

    [Fact]
    [Trait("Spec", "DSC-7")]
    public void SaleItems_AreLeftOutByDefault_AndCountOnlyWhenTheCodeSaysSo()
    {
        var lines = new[] { Line(1000m), Line(800m, sale: true) };

        Assert.Equal(100m, DiscountRule.Evaluate(Code(), lines, Now, 0, 0).Amount);
        Assert.Equal(180m, DiscountRule.Evaluate(Code(includeSale: true), lines, Now, 0, 0).Amount);
        Assert.Equal(DiscountRejection.NoEligibleLines, DiscountRule.Evaluate(Code(), [Line(800m, sale: true)], Now, 0, 0).Rejection);
    }

    [Theory]
    [Trait("Spec", "DSC-9")]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnknownOrDeactivatedCode_GivesTheSameReason(bool deactivated)
    {
        DiscountCode? code = null;
        if (deactivated)
        {
            code = Code();
            code.Deactivate();
        }

        Assert.Equal(DiscountRejection.Unknown, DiscountRule.Evaluate(code, [Line(1000m)], Now, 0, 0).Rejection);
    }

    [Fact]
    [Trait("Spec", "DSC-9")]
    public void TheChecksRunInAFixedOrder_SoTheReasonIsAlwaysTheSame()
    {
        var code = Code(expires: Now.AddHours(-1), cap: 1, min: 99999m, categories: [1]);

        Assert.Equal(DiscountRejection.Expired, DiscountRule.Evaluate(code, [Line(10m)], Now, 5, 5).Rejection);
    }

    [Fact]
    [Trait("Spec", "TOT-6")]
    public void APercentageOfOneHundred_NeverExceedsTheEligibleSubtotal()
    {
        var result = DiscountRule.Evaluate(Code(value: 100m), [Line(333.33m, 3)], Now, 0, 0);

        Assert.Equal(999.99m, result.Amount);
    }

    [Theory]
    [Trait("Spec", "DSC-12")]
    [InlineData("ab", 10)]
    [InlineData("HAS SPACE", 10)]
    [InlineData("BAD$CODE", 10)]
    [InlineData("OK-CODE", 0)]
    [InlineData("OK-CODE", 101)]
    [InlineData("OK-CODE", -5)]
    public void AnInvalidCode_IsRefusedWithAMessage(string code, decimal percent)
    {
        Assert.Throws<DomainException>(() => Code(code: code, value: percent));
    }

    [Fact]
    [Trait("Spec", "DSC-12")]
    public void OtherInvalidValues_AreRefused()
    {
        Assert.Throws<DomainException>(() => Code(kind: DiscountKind.Fixed, value: 0m));
        Assert.Throws<DomainException>(() => Code(kind: DiscountKind.Fixed, value: 2_000_000m));
        Assert.Throws<DomainException>(() => Code(min: -1m));
        Assert.Throws<DomainException>(() => Code(expires: Now.AddDays(-2)));
        Assert.Throws<DomainException>(() => Code(cap: 0));
        Assert.Throws<DomainException>(() => Code(perCustomer: 0));
        Assert.Throws<DomainException>(() => Code(categories: [0]));
    }

    [Fact]
    [Trait("Spec", "DSC-1")]
    public void ACodeIsStoredUpperCaseAndTrimmed_SoItCanBeTypedAnyWay()
    {
        var code = Code(code: "  welcome-10 ");

        Assert.Equal("WELCOME-10", code.Code);
        Assert.Equal("WELCOME-10", DiscountCode.Normalise(" welcome-10"));
    }

    [Fact]
    [Trait("Spec", "DSC-12")]
    public void Updating_ChangesTheScopeWithoutDuplicates()
    {
        var code = Code(categories: [1, 2]);

        code.Update("WELCOME10", DiscountKind.Fixed, 200m, null, null, null, 1, false, [2, 3, 3], Now);

        Assert.Equal(new short[] { 2, 3 }, code.Scope.Select(s => s.CategoryId).Order().ToArray());
        Assert.Equal(DiscountKind.Fixed, code.Kind);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public void ARedemption_GoesHeldConfirmedOrReleased_AndRepeatsChangeNothing()
    {
        var held = new Redemption(1, "key-1", "C7", 100m);
        Assert.True(held.CountsAsUse);

        held.Confirm("RMP-2026-00001-1");
        held.Confirm("RMP-2026-99999-9");
        Assert.Equal(RedemptionStatus.Confirmed, held.Status);
        Assert.Equal("RMP-2026-00001-1", held.OrderNumber);

        held.Release();
        held.Release();
        Assert.Equal(RedemptionStatus.Released, held.Status);
        Assert.False(held.CountsAsUse);

        var other = new Redemption(1, "key-2", "C7", 100m);
        other.Release();
        other.Confirm("X");
        Assert.Equal(RedemptionStatus.Released, other.Status);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public void ARedemptionNeedsAKeyAndACustomer()
    {
        Assert.Throws<DomainException>(() => new Redemption(1, " ", "C7", 1m));
        Assert.Throws<DomainException>(() => new Redemption(1, "k", "", 1m));
    }
}
