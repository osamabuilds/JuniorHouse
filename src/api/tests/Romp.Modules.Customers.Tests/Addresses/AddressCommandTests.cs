using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Application.Addresses;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Addresses;

/// <summary>customers/addresses A4 (ADD-1 to ADD-7): the address commands and the paged list through the real pipeline and database.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class AddressCommandTests(CustomersPostgres postgres)
{
    private static AddAddressCommand Add(string name = "Ayesha Khan", short city = FakeGeography.Lahore, short province = FakeGeography.Punjab, string phone = "0300 1234567") =>
        new(name, phone, province, city, "House 12, Street 4", null, null);

    [Fact]
    [Trait("Spec", "ADD-1")]
    [Trait("Spec", "ADD-2")]
    public async Task Add_TheFirstAddress_IsSavedAsTheDefault_WithItsNames()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();

        var dto = await harness.Send(customer, Add());

        Assert.True(dto.IsDefault);
        Assert.Equal("+923001234567", dto.Phone);
        Assert.Equal("Punjab", dto.ProvinceName);
        Assert.Equal("Lahore", dto.CityName);
        Assert.Single(await harness.StoredAsync(customer));
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task Add_ASecondAddress_LeavesTheDefaultAlone()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var first = await harness.Send(customer, Add("First Home"));

        var second = await harness.Send(customer, Add("Second Home"));

        Assert.False(second.IsDefault);
        Assert.Equal([first.Id], (await harness.StoredAsync(customer)).Where(r => r.IsDefault).Select(r => r.Id));
    }

    [Fact]
    [Trait("Spec", "ADD-6")]
    public async Task Add_AtTheLimit_IsRefusedAndNothingIsSaved()
    {
        using var harness = new AddressHarness(postgres, maxAddresses: 2);
        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, Add("One Home"));
        await harness.Send(customer, Add("Two Home"));

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Send(customer, Add("Three Home")));

        Assert.Contains("2", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await harness.StoredAsync(customer)).Count);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Add_ACityOutsideTheProvince_IsRejectedOnTheCityField()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => harness.Send(customer, Add(city: FakeGeography.Karachi)));

        Assert.Contains("CityId", error.Errors.Keys);
        Assert.Empty(await harness.StoredAsync(customer));
    }

    [Fact]
    [Trait("Spec", "ADD-5")]
    public async Task Add_AnInvalidPhone_IsRejectedOnThePhoneField()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => harness.Send(customer, Add(phone: "12345")));

        Assert.Contains("Phone", error.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "ADD-1")]
    public async Task Update_ChangesTheFields_AndKeepsTheDefault()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var saved = await harness.Send(customer, Add());

        var updated = await harness.Send(customer, new UpdateAddressCommand(saved.Id, "New Name", "0321 7654321", FakeGeography.Sindh, FakeGeography.Karachi, "Flat 5, Block 2", "Near the mall", "75500"));

        Assert.Equal("New Name", updated.RecipientName);
        Assert.Equal("Karachi", updated.CityName);
        Assert.True(updated.IsDefault);
        Assert.Equal("New Name", (await harness.StoredAsync(customer)).Single().Recipient);
    }

    [Fact]
    [Trait("Spec", "ADD-3")]
    public async Task AnotherCustomersAddress_IsNotFound_ForGetUpdateRemoveAndMakeDefault_AndNothingChanges()
    {
        using var harness = new AddressHarness(postgres);
        var owner = await harness.NewCustomerAsync();
        var intruder = await harness.NewCustomerAsync();
        var address = await harness.Send(owner, Add("Owner Home"));
        await harness.Send(owner, Add("Owner Other"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Send(intruder, new GetAddressQuery(address.Id)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Send(intruder, new UpdateAddressCommand(address.Id, "Hacked", "0300 1234567", 1, 10, "House 12, Street 4", null, null)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Send(intruder, new MakeDefaultAddressCommand(address.Id)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Send(intruder, new RemoveAddressCommand(address.Id)));

        var stored = await harness.StoredAsync(owner);
        Assert.Equal(2, stored.Count);
        Assert.Equal("Owner Home", stored[0].Recipient);
        Assert.True(stored[0].IsDefault);
        Assert.Empty(await harness.StoredAsync(intruder));
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task MakeDefault_MovesTheDefault_AndLeavesExactlyOne()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, Add("First Home"));
        var second = await harness.Send(customer, Add("Second Home"));

        var result = await harness.Send(customer, new MakeDefaultAddressCommand(second.Id));

        Assert.True(result.IsDefault);
        Assert.Equal([second.Id], (await harness.StoredAsync(customer)).Where(r => r.IsDefault).Select(r => r.Id));
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task Remove_TheDefault_PromotesTheMostRecentRemaining()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var first = await harness.Send(customer, Add("First Home"));
        await harness.Send(customer, Add("Second Home"));
        var third = await harness.Send(customer, Add("Third Home"));

        await harness.Send(customer, new RemoveAddressCommand(first.Id));

        var stored = await harness.StoredAsync(customer);
        Assert.Equal(2, stored.Count);
        Assert.Equal([third.Id], stored.Where(r => r.IsDefault).Select(r => r.Id));
    }

    [Fact]
    [Trait("Spec", "ADD-1")]
    public async Task Remove_ANonDefault_KeepsTheDefault_AndRemovingTheLastLeavesAnEmptyBook()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var first = await harness.Send(customer, Add("First Home"));
        var second = await harness.Send(customer, Add("Second Home"));

        await harness.Send(customer, new RemoveAddressCommand(second.Id));
        Assert.Equal([first.Id], (await harness.StoredAsync(customer)).Where(r => r.IsDefault).Select(r => r.Id));

        await harness.Send(customer, new RemoveAddressCommand(first.Id));
        Assert.Empty(await harness.StoredAsync(customer));
    }

    [Fact]
    [Trait("Spec", "ADD-7")]
    public async Task List_IsPaged_DefaultFirstThenNewest_LoadsNamesOncePerPage_AndShowsOnlyTheCallersOwn()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var other = await harness.NewCustomerAsync();
        await harness.Send(other, Add("Not Mine"));
        var first = await harness.Send(customer, Add("Address 1"));
        for (var i = 2; i <= 5; i++)
        {
            await harness.Send(customer, Add($"Address {i}", i % 2 == 0 ? FakeGeography.Lahore : FakeGeography.Multan));
        }

        harness.Geography.ResetCalls();
        var pageOne = await harness.Send(customer, new ListAddressesQuery(1, 2));
        var calls = harness.Geography.ByIdCalls;
        var pageTwo = await harness.Send(customer, new ListAddressesQuery(2, 2));
        var pageThree = await harness.Send(customer, new ListAddressesQuery(3, 2));

        Assert.Equal(5, pageOne.TotalCount);
        Assert.Equal(3, pageOne.TotalPages);
        Assert.Equal(["Address 1", "Address 5"], pageOne.Items.Select(a => a.RecipientName));
        Assert.Equal(first.Id, pageOne.Items[0].Id);
        Assert.Equal(["Address 4", "Address 3"], pageTwo.Items.Select(a => a.RecipientName));
        Assert.Equal(["Address 2"], pageThree.Items.Select(a => a.RecipientName));
        Assert.Equal(2, calls);
    }

    [Fact]
    [Trait("Spec", "ADD-7")]
    public async Task List_ClampsThePageSize()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, Add());

        var result = await harness.Send(customer, new ListAddressesQuery(0, 5000));

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task AGuest_CannotUseTheAddressBook()
    {
        using var harness = new AddressHarness(postgres);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.Send(null, Add()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.Send(null, new ListAddressesQuery()));
    }
}
