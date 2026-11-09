using Romp.BuildingBlocks.Domain;
using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class ShoppingCartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Guest = TimeSpan.FromDays(30);

    private static ShoppingCart Account() => ShoppingCart.Create(CartOwner.ForCustomer(7), Now, Guest);

    private static ShoppingCart GuestCart() => ShoppingCart.Create(CartOwner.ForGuest("hash-of-cookie"), Now, Guest);

    private static void Add(ShoppingCart cart, long variant, int quantity) =>
        cart.AddLine(variant, quantity, ShoppingCart.DefaultMaxPerLine, ShoppingCart.DefaultMaxLines, Now, Guest);

    [Fact]
    [Trait("Spec", "CRT-1")]
    public void AddingTheSameVariantTwice_MakesOneLineWithTheTotal()
    {
        var cart = Account();

        Add(cart, 101, 2);
        Add(cart, 101, 3);

        var line = Assert.Single(cart.Lines);
        Assert.Equal(5, line.Quantity);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public void SetQuantity_ChangesTheLine()
    {
        var cart = Account();
        Add(cart, 101, 2);

        cart.SetQuantity(101, 4, ShoppingCart.DefaultMaxPerLine, Now, Guest);

        Assert.Equal(4, Assert.Single(cart.Lines).Quantity);
    }

    [Fact]
    [Trait("Spec", "CRT-2")]
    public void RemovingTheLastLine_LeavesAnEmptyCart_AndDropsTheCode()
    {
        var cart = Account();
        Add(cart, 101, 1);
        cart.ApplyCode(9, Now, Guest);

        cart.RemoveLine(101, Now, Guest);

        Assert.True(cart.IsEmpty);
        Assert.Null(cart.AppliedCodeId);
    }

    [Fact]
    [Trait("Spec", "CRT-2")]
    public void RemovingAVariantThatIsNotThere_DoesNothing()
    {
        var cart = Account();
        Add(cart, 101, 1);

        cart.RemoveLine(999, Now, Guest);

        Assert.Single(cart.Lines);
    }

    [Theory]
    [Trait("Spec", "CRT-11")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void AQuantityOutsideOneToTheMaximum_IsRefused(int quantity)
    {
        var cart = Account();

        Assert.Throws<DomainException>(() => Add(cart, 101, quantity));
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    [Trait("Spec", "CRT-11")]
    public void AddingPastTheMaximumInTotal_IsRefused_AndKeepsTheOldQuantity()
    {
        var cart = Account();
        Add(cart, 101, 8);

        Assert.Throws<DomainException>(() => Add(cart, 101, 3));

        Assert.Equal(8, Assert.Single(cart.Lines).Quantity);
    }

    [Fact]
    [Trait("Spec", "CRT-11")]
    public void SettingTheQuantityToZero_IsRefused()
    {
        var cart = Account();
        Add(cart, 101, 2);

        Assert.Throws<DomainException>(() => cart.SetQuantity(101, 0, ShoppingCart.DefaultMaxPerLine, Now, Guest));
    }

    [Fact]
    [Trait("Spec", "CRT-11")]
    public void ANewVariantPastTheLineLimit_IsRefused()
    {
        var cart = Account();
        for (var v = 1; v <= 3; v++)
        {
            cart.AddLine(v, 1, 10, 3, Now, Guest);
        }

        Assert.Throws<DomainException>(() => cart.AddLine(4, 1, 10, 3, Now, Guest));
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public void Merge_CombinesAVariantInBothCarts_WithoutLossOrDuplication()
    {
        var account = Account();
        Add(account, 101, 2);
        Add(account, 202, 1);
        var guest = GuestCart();
        Add(guest, 101, 3);
        Add(guest, 303, 1);

        var notices = account.MergeFrom(guest, new Dictionary<long, int>(), 10, 50, Now, Guest);

        Assert.Empty(notices);
        Assert.Equal(3, account.Lines.Count);
        Assert.Equal(5, account.Lines.Single(l => l.VariantId == 101).Quantity);
        Assert.Equal(1, account.Lines.Single(l => l.VariantId == 202).Quantity);
        Assert.Equal(1, account.Lines.Single(l => l.VariantId == 303).Quantity);
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public void Merge_CapsAtAvailableStock_AndSaysSo()
    {
        var account = Account();
        Add(account, 101, 2);
        var guest = GuestCart();
        Add(guest, 101, 5);

        var notices = account.MergeFrom(guest, new Dictionary<long, int> { [101] = 4 }, 10, 50, Now, Guest);

        Assert.Equal(4, Assert.Single(account.Lines).Quantity);
        var notice = Assert.Single(notices);
        Assert.Equal(new MergeNotice(101, 7, 4), notice);
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public void Merge_NeverReducesWhatTheAccountCartAlreadyHeld()
    {
        var account = Account();
        Add(account, 101, 5);
        var guest = GuestCart();
        Add(guest, 101, 1);

        account.MergeFrom(guest, new Dictionary<long, int> { [101] = 2 }, 10, 50, Now, Guest);

        Assert.Equal(5, Assert.Single(account.Lines).Quantity);
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public void Merge_SkipsAVariantWithNoStock_AndSaysSo()
    {
        var account = Account();
        var guest = GuestCart();
        Add(guest, 101, 2);

        var notices = account.MergeFrom(guest, new Dictionary<long, int> { [101] = 0 }, 10, 50, Now, Guest);

        Assert.True(account.IsEmpty);
        Assert.Equal(new MergeNotice(101, 2, 0), Assert.Single(notices));
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public void AGuestCart_ExpiresAfterItsLifetime_AndUseExtendsIt()
    {
        var cart = GuestCart();

        Assert.False(cart.IsExpired(Now.AddDays(29)));
        Assert.True(cart.IsExpired(Now.AddDays(30)));

        cart.AddLine(101, 1, 10, 50, Now.AddDays(20), Guest);

        Assert.False(cart.IsExpired(Now.AddDays(45)));
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public void AnAccountCart_NeverExpires()
    {
        Assert.False(Account().IsExpired(Now.AddYears(5)));
    }

    [Fact]
    [Trait("Spec", "DSC-13")]
    public void ACodeCannotBeAppliedToAnEmptyCart()
    {
        Assert.Throws<DomainException>(() => Account().ApplyCode(9, Now, Guest));
    }

    [Fact]
    [Trait("Spec", "CRT-9")]
    public void AnOwnerHasExactlyOneIdentity()
    {
        var customer = CartOwner.ForCustomer(7);
        var guest = CartOwner.ForGuest("h");

        Assert.Equal(7, customer.CustomerId);
        Assert.Null(customer.GuestKeyHash);
        Assert.Null(guest.CustomerId);
        Assert.True(guest.IsGuest);
        Assert.Throws<ArgumentOutOfRangeException>(() => CartOwner.ForCustomer(0));
        Assert.Throws<ArgumentException>(() => CartOwner.ForGuest(" "));
    }

    [Fact]
    public void Clear_EmptiesTheCartAndTheCode()
    {
        var cart = Account();
        Add(cart, 101, 2);
        cart.ApplyCode(9, Now, Guest);

        cart.Clear(Now, Guest);

        Assert.True(cart.IsEmpty);
        Assert.Null(cart.AppliedCodeId);
    }
}
