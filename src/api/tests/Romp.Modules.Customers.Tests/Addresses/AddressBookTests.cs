using System.Reflection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Domain.Addresses;

namespace Romp.Modules.Customers.Tests.Addresses;

/// <summary>customers/addresses A2 (ADD-2, ADD-5, ADD-6): the rules of a single customer's address book, with no database.</summary>
public sealed class AddressBookTests
{
    private static CustomerAddress Address(long id, string name = "Ayesha Khan") =>
        WithId(CustomerAddress.Create(7, name, "0300 1234567", 1, 2, "House 12, Street 4", null, null), id);

    private static CustomerAddress WithId(CustomerAddress address, long id)
    {
        typeof(Entity<long>).GetProperty(nameof(Entity<long>.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(address, id);
        return address;
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void Add_TheFirstAddress_BecomesTheDefault()
    {
        var first = Address(1);

        AddressBook.Add([], first, maxAddresses: 10);

        Assert.True(first.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void Add_AFurtherAddress_DoesNotTakeTheDefault()
    {
        var first = Address(1);
        AddressBook.Add([], first, 10);
        var second = Address(2);

        AddressBook.Add([first], second, 10);

        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-6")]
    public void Add_AtTheLimit_IsRefusedWithAClearMessage()
    {
        var book = Enumerable.Range(1, 3).Select(i => Address(i)).ToList();

        var error = Assert.Throws<DomainException>(() => AddressBook.Add(book, Address(4), maxAddresses: 3));

        Assert.Contains("3", error.Message, StringComparison.Ordinal);
        Assert.Contains("remove", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void MakeDefault_MovesTheDefault_AndLeavesExactlyOne()
    {
        var a = Address(1);
        var b = Address(2);
        var c = Address(3);
        AddressBook.Add([], a, 10);
        AddressBook.Add([a], b, 10);
        AddressBook.Add([a, b], c, 10);

        AddressBook.MakeDefault([a, b, c], c);

        Assert.Equal([false, false, true], new[] { a.IsDefault, b.IsDefault, c.IsDefault });
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void MakeDefault_OnTheCurrentDefault_ChangesNothing()
    {
        var a = Address(1);
        var b = Address(2);
        AddressBook.Add([], a, 10);
        AddressBook.Add([a], b, 10);

        AddressBook.MakeDefault([a, b], a);

        Assert.True(a.IsDefault);
        Assert.False(b.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void Remove_TheDefault_PromotesTheMostRecentRemaining()
    {
        var a = Address(1);
        var b = Address(5);
        var c = Address(3);
        AddressBook.Add([], a, 10);
        AddressBook.Add([a], b, 10);
        AddressBook.Add([a, b], c, 10);

        var promoted = AddressBook.Remove([a, b, c], a);

        Assert.Same(b, promoted);
        Assert.True(b.IsDefault);
        Assert.False(c.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void Remove_ANonDefault_KeepsTheDefault_AndPromotesNobody()
    {
        var a = Address(1);
        var b = Address(2);
        AddressBook.Add([], a, 10);
        AddressBook.Add([a], b, 10);

        var promoted = AddressBook.Remove([a, b], b);

        Assert.Null(promoted);
        Assert.True(a.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public void Remove_TheOnlyAddress_LeavesAnEmptyBook()
    {
        var a = Address(1);
        AddressBook.Add([], a, 10);

        Assert.Null(AddressBook.Remove([a], a));
    }

    [Fact]
    [Trait("Spec", "ADD-5")]
    public void Create_StoresThePhoneInItsNormalisedForm_AndTrimsTheText()
    {
        var address = CustomerAddress.Create(7, "  Ayesha Khan ", "0300-1234567", 1, 2, "  House 12, Street 4 ", "  ", " 54000 ");

        Assert.Equal("+923001234567", address.Phone);
        Assert.Equal("Ayesha Khan", address.RecipientName);
        Assert.Equal("House 12, Street 4", address.LineOne);
        Assert.Null(address.LineTwo);
        Assert.Equal("54000", address.Postcode);
    }

    [Theory]
    [InlineData("A", "0300 1234567", "House 12, Street 4", null, null)]
    [InlineData("Ayesha Khan", "12345", "House 12, Street 4", null, null)]
    [InlineData("Ayesha Khan", "0300 1234567", "Home", null, null)]
    [InlineData("Ayesha Khan", "0300 1234567", "House 12, Street 4", null, "54 000 000 000")]
    [InlineData("Ayesha Khan", "0300 1234567", "House 12, Street 4", null, "5400!")]
    public void Create_RejectsValuesOutsideTheRules(string name, string phone, string lineOne, string? lineTwo, string? postcode)
    {
        Assert.Throws<ArgumentException>(() => CustomerAddress.Create(7, name, phone, 1, 2, lineOne, lineTwo, postcode));
    }

    [Fact]
    public void Create_RejectsAnOverlongRecipientAndLines()
    {
        Assert.Throws<ArgumentException>(() => CustomerAddress.Create(7, new string('a', 81), "0300 1234567", 1, 2, "House 12, Street 4", null, null));
        Assert.Throws<ArgumentException>(() => CustomerAddress.Create(7, "Ayesha Khan", "0300 1234567", 1, 2, new string('a', 151), null, null));
        Assert.Throws<ArgumentException>(() => CustomerAddress.Create(7, "Ayesha Khan", "0300 1234567", 1, 2, "House 12, Street 4", new string('a', 151), null));
    }
}
