using Romp.Modules.Customers.Application.Addresses;
using Romp.Modules.Customers.Infrastructure.Addresses;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Addresses;

/// <summary>customers/addresses A6 (FR-40): what checkout will read through <c>ICustomerAddressQueries</c>, always scoped to the signed-in customer.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class AddressContractTests(CustomersPostgres postgres)
{
    private static AddAddressCommand Add(string name) => new(name, "0300 1234567", FakeGeography.Punjab, FakeGeography.Lahore, "House 12, Street 4", null, "54000");

    private CustomerAddressQueries QueriesFor(long? customerId) =>
        new(postgres.NewContext(), new AddressHarness.TestCurrentCustomer { CustomerId = customerId });

    [Fact]
    [Trait("Spec", "FR-40")]
    public async Task GetMyAddresses_ReturnsOnlyTheCallersAddresses_DefaultFirst()
    {
        using var harness = new AddressHarness(postgres);
        var mine = await harness.NewCustomerAsync();
        var other = await harness.NewCustomerAsync();
        var first = await harness.Send(mine, Add("First Home"));
        var second = await harness.Send(mine, Add("Second Home"));
        await harness.Send(other, Add("Not Mine"));

        var addresses = await QueriesFor(mine).GetMyAddressesAsync(CancellationToken.None);

        Assert.Equal([first.Id, second.Id], addresses.Select(a => a.Id));
        Assert.True(addresses[0].IsDefault);
        Assert.Equal("+923001234567", addresses[0].Phone);
        Assert.Equal(FakeGeography.Lahore, addresses[0].CityId);
        Assert.Equal("54000", addresses[0].Postcode);
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task GetMyAddress_ForSomeoneElsesId_IsNull_AndAGuestSeesNothing()
    {
        using var harness = new AddressHarness(postgres);
        var mine = await harness.NewCustomerAsync();
        var other = await harness.NewCustomerAsync();
        var theirs = await harness.Send(other, Add("Not Mine"));
        var own = await harness.Send(mine, Add("Mine"));

        Assert.Null(await QueriesFor(mine).GetMyAddressAsync(theirs.Id, CancellationToken.None));
        Assert.Equal(own.Id, (await QueriesFor(mine).GetMyAddressAsync(own.Id, CancellationToken.None))!.Id);
        Assert.Empty(await QueriesFor(null).GetMyAddressesAsync(CancellationToken.None));
        Assert.Null(await QueriesFor(null).GetMyAddressAsync(own.Id, CancellationToken.None));
    }
}
