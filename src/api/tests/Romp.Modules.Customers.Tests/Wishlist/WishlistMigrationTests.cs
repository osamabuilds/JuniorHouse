using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Wishlist;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Wishlist;

/// <summary>customers/wishlist W3 (WSL-1, WSL-4): the wishlist table on a real PostgreSQL, and the index that makes a second row for one product impossible.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class WishlistMigrationTests(CustomersPostgres postgres)
{
    private async Task<long> NewCustomerAsync()
    {
        await using var context = postgres.NewContext();
        var customer = Customer.Create($"wish.{Guid.NewGuid():N}@example.test", "Wish", "Test");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    [Fact]
    [Trait("Spec", "WSL-1")]
    public async Task TheWishlistTable_Exists_AndRoundTripsAnItem()
    {
        var customerId = await NewCustomerAsync();
        long id;
        await using (var context = postgres.NewContext())
        {
            var item = WishlistItem.Create(customerId, 42, 1999.50m);
            context.Wishlist.Add(item);
            await context.SaveChangesAsync();
            id = item.Id;
        }

        await using var read = postgres.NewContext();
        var saved = await read.Wishlist.AsNoTracking().SingleAsync(w => w.Id == id);
        Assert.Equal(customerId, saved.CustomerId);
        Assert.Equal(42, saved.ProductId);
        Assert.Equal(1999.50m, saved.SavedPrice);
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public async Task ASecondRow_ForTheSameCustomerAndProduct_IsRefusedByTheDatabase()
    {
        var customerId = await NewCustomerAsync();
        await using var context = postgres.NewContext();
        context.Wishlist.Add(WishlistItem.Create(customerId, 7, 100m));
        await context.SaveChangesAsync();

        await using var second = postgres.NewContext();
        second.Wishlist.Add(WishlistItem.Create(customerId, 7, 100m));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Equal("UQ_CUST_WSHL_CUST_ID_PRDT_ID", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [Fact]
    [Trait("Spec", "WSL-1")]
    public async Task TheSameProduct_ForTwoCustomers_IsAllowed()
    {
        var first = await NewCustomerAsync();
        var second = await NewCustomerAsync();
        await using var context = postgres.NewContext();
        context.Wishlist.AddRange(WishlistItem.Create(first, 9, 100m), WishlistItem.Create(second, 9, 100m));

        await context.SaveChangesAsync();
    }
}
