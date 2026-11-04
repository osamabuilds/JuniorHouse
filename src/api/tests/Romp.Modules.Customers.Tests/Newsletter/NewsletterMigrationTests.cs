using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Newsletter;

/// <summary>customers/newsletter NL2 (NLS-1, NLS-5): the consent tables on a real PostgreSQL, their seeded lookups and the unique address.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class NewsletterMigrationTests(CustomersPostgres postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static string NewEmail() => $"nl.{Guid.NewGuid():N}@example.test";

    [Fact]
    [Trait("Spec", "NLS-5")]
    public async Task ASubscription_RoundTrips_WithItsHistoryRow()
    {
        var email = NewEmail();
        long id;
        await using (var context = postgres.NewContext())
        {
            var subscription = NewsletterSubscription.Start(email, ConsentSource.Footer, Now);
            context.Newsletter.Add(subscription);
            await context.SaveChangesAsync();
            id = subscription.Id;
        }

        await using var read = postgres.NewContext();
        var saved = await read.Newsletter.AsNoTracking().Include(s => s.History).SingleAsync(s => s.Id == id);
        Assert.Equal(email, saved.Email);
        Assert.Equal(NewsletterStatus.Subscribed, saved.Status);
        var row = Assert.Single(saved.History);
        Assert.Equal((ConsentAction.Subscribed, ConsentSource.Footer), (row.Action, row.Source));
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public async Task ASecondRecord_ForTheSameAddress_IsRefusedByTheDatabase()
    {
        var email = NewEmail();
        await using (var context = postgres.NewContext())
        {
            context.Newsletter.Add(NewsletterSubscription.Start(email, ConsentSource.Footer, Now));
            await context.SaveChangesAsync();
        }

        await using var second = postgres.NewContext();
        second.Newsletter.Add(NewsletterSubscription.Start(email, ConsentSource.Account, Now));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Equal("UQ_NWSL_SUBS_EML", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public async Task AnAddress_NotInLowerCase_IsRefusedByTheDatabase()
    {
        await using var context = postgres.NewContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CUST\".\"NWSL_SUBS\" (\"EML\", \"STS_ID\", \"CHNG_DTE\", \"INSR_DTE\", \"INSR_BY\") VALUES ('Mixed@Example.test', 1, now(), now(), 'test')"));

        Assert.Equal("CK_NWSL_SUBS_EML_LOWR", ex.ConstraintName);
    }

    [Fact]
    [Trait("Spec", "NLS-5")]
    public async Task TheLookups_AreSeeded_WithTheStableIds()
    {
        await using var context = postgres.NewContext();
        var statuses = await Codes(context, "NWSL_STS_LKP");
        var actions = await Codes(context, "NWSL_ACTN_LKP");
        var sources = await Codes(context, "NWSL_SRC_LKP");

        Assert.Equal([(1, "SUBD"), (2, "UNSB")], statuses);
        Assert.Equal([(1, "SUBD"), (2, "UNSB")], actions);
        Assert.Equal([(1, "REGN"), (2, "ACCT"), (3, "FOOT"), (4, "LINK")], sources);
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public async Task Unsubscribing_AndSubscribingAgain_KeepsOneRecordWithAllThreeHistoryRows()
    {
        var email = NewEmail();
        long id;
        await using (var context = postgres.NewContext())
        {
            var subscription = NewsletterSubscription.Start(email, ConsentSource.Footer, Now);
            context.Newsletter.Add(subscription);
            await context.SaveChangesAsync();
            id = subscription.Id;
        }

        await using (var context = postgres.NewContext())
        {
            var subscription = await context.Newsletter.Include(s => s.History).SingleAsync(s => s.Id == id);
            subscription.Unsubscribe(ConsentSource.Link, Now.AddDays(1));
            await context.SaveChangesAsync();
        }

        await using (var context = postgres.NewContext())
        {
            var subscription = await context.Newsletter.Include(s => s.History).SingleAsync(s => s.Id == id);
            subscription.Subscribe(ConsentSource.Account, Now.AddDays(2));
            await context.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        var saved = await read.Newsletter.AsNoTracking().Include(s => s.History).SingleAsync(s => s.Email == email);
        Assert.Equal(
            [ConsentAction.Subscribed, ConsentAction.Unsubscribed, ConsentAction.Subscribed],
            saved.History.OrderBy(h => h.Id).Select(h => h.Action));
    }

    private static async Task<List<(int Id, string Code)>> Codes(Romp.Modules.Customers.Infrastructure.Persistence.CustDbContext context, string table)
    {
        var rows = new List<(int, string)>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"ID\", \"CODE\" FROM \"CUST\".\"{table}\" ORDER BY \"ID\"";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt16(0), reader.GetString(1)));
        }

        return rows;
    }
}
