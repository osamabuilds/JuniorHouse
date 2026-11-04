using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>The customer record (customers/accounts, Sprint 10 slice): its rules, its table, and the seeded stand-in.</summary>
public sealed class CustomerTests
{
    [Fact]
    [Trait("Spec", "ACT-14")]
    public void Create_StoresTheEmailTrimmedAndLowerCase()
    {
        var customer = Customer.Create("  Dev.Customer@Example.TEST ", " Dev ", " Customer ", " 03001234567 ");

        Assert.Equal("dev.customer@example.test", customer.Email);
        Assert.Equal("Dev", customer.FirstName);
        Assert.Equal("Customer", customer.LastName);
        Assert.Equal("03001234567", customer.Phone);
    }

    [Theory]
    [Trait("Spec", "ACT-14")]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@no-name.test")]
    [InlineData("two@@signs.test")]
    [InlineData("two@at@signs.test")]
    [InlineData("white space@example.test")]
    [InlineData("ends-with-at@")]
    public void Create_RefusesSomethingThatIsNotAnEmailAddress(string email)
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(email, "A", "B"));
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public void Create_RefusesBlankOrOverlongNames()
    {
        Assert.Throws<ArgumentException>(() => Customer.Create("a@example.test", " ", "B"));
        Assert.Throws<ArgumentException>(() => Customer.Create("a@example.test", "A", new string('x', Customer.NameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => Customer.Create("a@example.test", "Line\nbreak", "B"));
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public void Create_TreatsABlankPhoneAsNone()
    {
        Assert.Null(Customer.Create("a@example.test", "A", "B", "   ").Phone);
    }
}

[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class CustomerMigrationTests(CustomersPostgres postgres)
{
    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task Applies_AndCreatesTheCustomerTable()
    {
        await using var context = postgres.NewContext();

        var tables = await context.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'CUST'")
            .ToListAsync();

        Assert.Contains("CUST_MAIN", tables);
        Assert.Contains("OUTB_MSG", tables);
        Assert.Contains("INBX", tables);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task TheEmailIsUnique_WhateverTheCaseOfTheOriginalInput()
    {
        var email = $"unique.{Guid.NewGuid():N}@example.test";
        await using (var context = postgres.NewContext())
        {
            context.Customers.Add(Customer.Create(email.ToUpperInvariant(), "A", "B"));
            await context.SaveChangesAsync();
        }

        await using var again = postgres.NewContext();
        again.Customers.Add(Customer.Create(email, "C", "D"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
        Assert.IsType<PostgresException>(error.InnerException);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task TheStandInAndTheOtherCustomer_AreSeededOnce()
    {
        await using (var context = postgres.NewContext())
        {
            await StandInCustomer.SeedAsync(context, CancellationToken.None);
            await StandInCustomer.SeedAsync(context, CancellationToken.None);
        }

        await using var read = postgres.NewContext();
        var emails = await read.Customers.AsNoTracking()
            .Where(c => c.Email == StandInCustomer.Email || c.Email == StandInCustomer.OtherEmail)
            .Select(c => c.Email).ToListAsync();
        Assert.Equal(2, emails.Count);
    }
}

[CollectionDefinition(Name)]
public sealed class CustomersPostgresFixtureDefinition : ICollectionFixture<CustomersPostgres>
{
    public const string Name = "Customers PostgreSQL";
}
