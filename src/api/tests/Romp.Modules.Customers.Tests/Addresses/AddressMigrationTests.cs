using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Addresses;

/// <summary>customers/addresses A3 (ADD-2, ADD-5): the address table on a real PostgreSQL, and the index that makes a second default impossible.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class AddressMigrationTests(CustomersPostgres postgres)
{
    private async Task<long> NewCustomerAsync()
    {
        await using var context = postgres.NewContext();
        var customer = Customer.Create($"addr.{Guid.NewGuid():N}@example.test", "Addr", "Test");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private static CustomerAddress NewAddress(long customerId, bool makeDefault)
    {
        var address = CustomerAddress.Create(customerId, "Ayesha Khan", "0300 1234567", 1, 2, "House 12, Street 4", null, null);
        AddressBook.Add(makeDefault ? [] : [CustomerAddress.Create(customerId, "Other Person", "0300 1234567", 1, 2, "House 1, Street 1", null, null)], address, 10);
        return address;
    }

    [Fact]
    [Trait("Spec", "ADD-1")]
    public async Task TheAddressTable_Exists_AndRoundTripsAnAddress()
    {
        var customerId = await NewCustomerAsync();
        long id;
        await using (var context = postgres.NewContext())
        {
            var address = CustomerAddress.Create(customerId, "Ayesha Khan", "0300 1234567", 3, 8, "House 12, Street 4", "Near the park", "54000");
            context.Addresses.Add(address);
            await context.SaveChangesAsync();
            id = address.Id;
        }

        await using var read = postgres.NewContext();
        var saved = await read.Addresses.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal(customerId, saved.CustomerId);
        Assert.Equal("+923001234567", saved.Phone);
        Assert.Equal((short)3, saved.ProvinceId);
        Assert.Equal((short)8, saved.CityId);
        Assert.Equal("Near the park", saved.LineTwo);
        Assert.Equal("54000", saved.Postcode);
        Assert.False(saved.IsDefault);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task ASecondDefault_ForTheSameCustomer_IsRefusedByTheDatabase()
    {
        var customerId = await NewCustomerAsync();
        await using var context = postgres.NewContext();
        context.Addresses.Add(NewAddress(customerId, makeDefault: true));
        context.Addresses.Add(NewAddress(customerId, makeDefault: true));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var inner = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal("23505", inner.SqlState);
        Assert.Equal("UQ_CUST_ADDR_DFLT", inner.ConstraintName);
    }

    [Fact]
    [Trait("Spec", "ADD-2")]
    public async Task ManyNonDefaults_AndADefaultPerCustomer_AreAllowed()
    {
        var first = await NewCustomerAsync();
        var second = await NewCustomerAsync();
        await using var context = postgres.NewContext();
        context.Addresses.AddRange(
            NewAddress(first, makeDefault: true), NewAddress(first, makeDefault: false), NewAddress(first, makeDefault: false),
            NewAddress(second, makeDefault: true));

        await context.SaveChangesAsync();

        Assert.Equal(3, await context.Addresses.CountAsync(a => a.CustomerId == first));
        Assert.Equal(1, await context.Addresses.CountAsync(a => a.CustomerId == first && a.IsDefault));
    }

    [Theory]
    [Trait("Spec", "ADD-5")]
    [InlineData("RCPT_NAME", "A")]
    [InlineData("PHON", "0300 1234567")]
    [InlineData("PHON", "+92100123456")]
    [InlineData("LINE_ONE", "Home")]
    [InlineData("PCDE", "54 000")]
    public async Task TheDatabase_RefusesValuesThatBreakTheRules_EvenIfTheDomainIsBypassed(string column, string value)
    {
        var customerId = await NewCustomerAsync();
        await using var context = postgres.NewContext();
        var sql = $"INSERT INTO \"CUST\".\"CUST_ADDR\" (\"CUST_ID\", \"RCPT_NAME\", \"PHON\", \"PROV_ID\", \"CITY_ID\", \"LINE_ONE\", \"PCDE\", \"DFLT_IND\", \"INSR_DTE\", \"INSR_BY\") " +
            "VALUES (@c, 'Ayesha Khan', '+923001234567', 1, 2, 'House 12, Street 4', NULL, false, now(), 'test')";
        sql = column switch
        {
            "PCDE" => sql.Replace("NULL", "@v", StringComparison.Ordinal),
            "RCPT_NAME" => sql.Replace("'Ayesha Khan'", "@v", StringComparison.Ordinal),
            "PHON" => sql.Replace("'+923001234567'", "@v", StringComparison.Ordinal),
            _ => sql.Replace("'House 12, Street 4'", "@v", StringComparison.Ordinal),
        };

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(sql, new NpgsqlParameter("c", customerId), new NpgsqlParameter("v", value)));

        Assert.Equal("23514", error.SqlState);
    }

    [Fact]
    [Trait("Spec", "ADD-1")]
    public async Task DeletingACustomer_DeletesTheirAddresses()
    {
        var customerId = await NewCustomerAsync();
        await using (var context = postgres.NewContext())
        {
            context.Addresses.Add(NewAddress(customerId, makeDefault: true));
            await context.SaveChangesAsync();
        }

        await using var cleanup = postgres.NewContext();
        await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"CUST\".\"CUST_MAIN\" WHERE \"ID\" = @p0", new NpgsqlParameter("p0", customerId));

        Assert.Equal(0, await cleanup.Addresses.CountAsync(a => a.CustomerId == customerId));
    }

    [Fact]
    [Trait("Spec", "ADD-7")]
    public async Task TheCustomerForeignKey_IsDeclaredAndIndexed()
    {
        await using var context = postgres.NewContext();

        var names = await context.Database.SqlQueryRaw<string>(
            "SELECT conname AS \"Value\" FROM pg_constraint WHERE conrelid = '\"CUST\".\"CUST_ADDR\"'::regclass AND contype = 'f' " +
            "UNION ALL SELECT indexname FROM pg_indexes WHERE schemaname = 'CUST' AND tablename = 'CUST_ADDR'").ToListAsync();

        Assert.Contains("FK_CUST_ADDR_CUST_ID", names);
        Assert.Contains("IX_CUST_ADDR_CUST_ID", names);
        Assert.Contains("UQ_CUST_ADDR_DFLT", names);
    }
}
