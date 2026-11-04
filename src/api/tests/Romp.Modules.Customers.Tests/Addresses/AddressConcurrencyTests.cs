using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Application.Addresses;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Addresses;

/// <summary>customers/addresses A4 (ADD-2, ADD-6): parallel requests for one customer cannot break the limit or the single default.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class AddressConcurrencyTests(CustomersPostgres postgres)
{
    private static AddAddressCommand Add(string name) => new(name, "0300 1234567", FakeGeography.Punjab, FakeGeography.Lahore, "House 12, Street 4", null, null);

    [Fact]
    [Trait("Spec", "ADD-6")]
    [Trait("Spec", "ADD-2")]
    public async Task ParallelAdds_NeverExceedTheLimit_AndLeaveExactlyOneDefault()
    {
        using var harness = new AddressHarness(postgres, maxAddresses: 5);
        var customer = await harness.NewCustomerAsync();

        var results = await Task.WhenAll(Enumerable.Range(1, 12).Select(async i =>
        {
            try
            {
                await harness.Send(customer, Add($"Parallel {i}"));
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }));

        var stored = await harness.StoredAsync(customer);
        Assert.Equal(5, results.Count(saved => saved));
        Assert.Equal(5, stored.Count);
        Assert.Single(stored, r => r.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task ParallelMakeDefault_LeavesExactlyOneDefault()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var ids = new List<long>();
        for (var i = 1; i <= 4; i++)
        {
            ids.Add((await harness.Send(customer, Add($"Home {i}"))).Id);
        }

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => harness.Send(customer, new MakeDefaultAddressCommand(ids[i % ids.Count]))));

        Assert.Single(await harness.StoredAsync(customer), r => r.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task ParallelRemoveAndMakeDefault_NeverLeavesTwoDefaults()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var ids = new List<long>();
        for (var i = 1; i <= 6; i++)
        {
            ids.Add((await harness.Send(customer, Add($"Home {i}"))).Id);
        }

        var work = ids.Take(3).Select(id => harness.Send(customer, new RemoveAddressCommand(id)))
            .Concat(ids.Skip(3).Select(id => harness.Send(customer, new MakeDefaultAddressCommand(id))));
        await Task.WhenAll(work);

        var stored = await harness.StoredAsync(customer);
        Assert.Equal(3, stored.Count);
        Assert.Single(stored, r => r.IsDefault);
    }
}
