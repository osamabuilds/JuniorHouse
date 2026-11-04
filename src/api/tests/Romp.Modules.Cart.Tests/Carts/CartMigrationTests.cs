using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class CartMigrationTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Guest = TimeSpan.FromDays(30);

    private async Task<int> ExecuteAsync(string sql)
    {
        await using var context = postgres.NewContext();
        return await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static string Insert(string customerId, string guestHash) =>
        "INSERT INTO \"CART\".\"CART_MAIN\" (\"CUST_ID\", \"GST_KEY_HASH\", \"LAST_USED_DTE\", \"INSR_DTE\", \"INSR_BY\") " +
        $"VALUES ({customerId}, {guestHash}, now(), now(), 'test')";

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task ACartWithLines_RoundTripsThroughTheDatabase()
    {
        await using (var context = postgres.NewContext())
        {
            var cart = ShoppingCart.Create(CartOwner.ForCustomer(4001), Now, Guest);
            cart.AddLine(11, 2, 10, 50, Now, Guest);
            cart.AddLine(12, 1, 10, 50, Now, Guest);
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        var loaded = await read.Carts.Include(c => c.Lines).SingleAsync(c => c.CustomerId == 4001);
        Assert.Equal(2, loaded.Lines.Count);
        Assert.Equal(2, loaded.Lines.Single(l => l.VariantId == 11).Quantity);
    }

    [Theory]
    [Trait("Spec", "CRT-9")]
    [InlineData("NULL", "NULL")]
    [InlineData("5001", "'hash-both'")]
    public async Task ACartNeedsExactlyOneOwner(string customerId, string guestHash)
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(Insert(customerId, guestHash)));

        Assert.Contains("CK_CART_MAIN_OWNR", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CRT-9")]
    public async Task AnOwnerHasAtMostOneCart()
    {
        await ExecuteAsync(Insert("6001", "NULL"));
        await ExecuteAsync(Insert("NULL", "'guest-a'"));

        var customer = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(Insert("6001", "NULL")));
        var guest = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(Insert("NULL", "'guest-a'")));

        Assert.Contains("UQ_CART_MAIN_CUST_ID", customer.ToString(), StringComparison.Ordinal);
        Assert.Contains("UQ_CART_MAIN_GST_KEY_HASH", guest.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CRT-11")]
    public async Task ALineNeedsAPositiveQuantity_AndOneLinePerVariant()
    {
        await ExecuteAsync(Insert("7001", "NULL"));
        var cartId = await ScalarAsync("SELECT \"ID\" FROM \"CART\".\"CART_MAIN\" WHERE \"CUST_ID\" = 7001");
        string Line(int quantity) =>
            "INSERT INTO \"CART\".\"CART_LINE\" (\"CART_ID\", \"VRNT_ID\", \"QTY\", \"INSR_DTE\", \"INSR_BY\") " +
            $"VALUES ({cartId}, 99, {quantity}, now(), 'test')";

        var zero = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(Line(0)));
        await ExecuteAsync(Line(2));
        var duplicate = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(Line(3)));

        Assert.Contains("CK_CART_LINE_QTY", zero.ToString(), StringComparison.Ordinal);
        Assert.Contains("UQ_CART_LINE_CART_ID_VRNT_ID", duplicate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CRT-2")]
    public async Task DeletingACart_DeletesItsLines()
    {
        await ExecuteAsync(Insert("8001", "NULL"));
        var cartId = await ScalarAsync("SELECT \"ID\" FROM \"CART\".\"CART_MAIN\" WHERE \"CUST_ID\" = 8001");
        await ExecuteAsync(
            "INSERT INTO \"CART\".\"CART_LINE\" (\"CART_ID\", \"VRNT_ID\", \"QTY\", \"INSR_DTE\", \"INSR_BY\") " +
            $"VALUES ({cartId}, 1, 1, now(), 'test')");

        await ExecuteAsync($"DELETE FROM \"CART\".\"CART_MAIN\" WHERE \"ID\" = {cartId}");

        Assert.Equal(0, await ScalarAsync($"SELECT count(*) FROM \"CART\".\"CART_LINE\" WHERE \"CART_ID\" = {cartId}"));
    }

    [Fact]
    public async Task TheModuleHasItsOwnOutboxAndInbox()
    {
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'CART' AND table_name = 'OUTB_MSG'"));
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'CART' AND table_name = 'INBX'"));
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
