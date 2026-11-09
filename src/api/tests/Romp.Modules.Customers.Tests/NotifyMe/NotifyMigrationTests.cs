using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N3 (NTM-2, NTM-3): the request table on a real PostgreSQL, its seeded lookup and the one-open-request rule.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class NotifyMigrationTests(CustomersPostgres postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    private static long NewVariant() => Random.Shared.NextInt64(1_000_000, long.MaxValue / 2);

    [Fact]
    [Trait("Spec", "NTM-2")]
    public async Task ARequest_RoundTrips()
    {
        var email = NewEmail();
        var variant = NewVariant();
        long id;
        await using (var context = postgres.NewContext())
        {
            var request = NotifyRequest.Open(variant, email, Now);
            context.NotifyRequests.Add(request);
            await context.SaveChangesAsync();
            id = request.Id;
        }

        await using var read = postgres.NewContext();
        var saved = await read.NotifyRequests.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal((variant, email, NotifyStatus.Open), (saved.VariantId, saved.Email, saved.Status));
        Assert.Null(saved.SentAt);
    }

    [Fact]
    [Trait("Spec", "NTM-3")]
    public async Task ASecondOpenRequest_ForTheSameVariantAndAddress_IsRefusedByTheDatabase()
    {
        var email = NewEmail();
        var variant = NewVariant();
        await using (var context = postgres.NewContext())
        {
            context.NotifyRequests.Add(NotifyRequest.Open(variant, email, Now));
            await context.SaveChangesAsync();
        }

        await using var second = postgres.NewContext();
        second.NotifyRequests.Add(NotifyRequest.Open(variant, email, Now));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Equal("UQ_NTFM_REQ_VRNT_ID_EML_OPEN", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [Fact]
    [Trait("Spec", "NTM-3")]
    public async Task ANewRequest_IsAllowed_OnceTheEarlierOneIsClosed_AndForAnotherVariant()
    {
        var email = NewEmail();
        var variant = NewVariant();
        await using var context = postgres.NewContext();
        var first = NotifyRequest.Open(variant, email, Now);
        context.NotifyRequests.Add(first);
        context.NotifyRequests.Add(NotifyRequest.Open(variant + 1, email, Now));
        await context.SaveChangesAsync();

        first.MarkNotified(Now);
        await context.SaveChangesAsync();
        context.NotifyRequests.Add(NotifyRequest.Open(variant, email, Now));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.NotifyRequests.CountAsync(r => r.VariantId == variant));
    }

    [Fact]
    [Trait("Spec", "NTM-2")]
    public async Task AnAddress_NotInLowerCase_IsRefusedByTheDatabase()
    {
        await using var context = postgres.NewContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CUST\".\"NTFM_REQ\" (\"VRNT_ID\", \"EML\", \"STS_ID\", \"INSR_DTE\", \"INSR_BY\") VALUES (1, 'Mixed@Example.test', 1, now(), 'test')"));

        Assert.Equal("CK_NTFM_REQ_EML_LOWR", ex.ConstraintName);
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task TheStatusLookup_IsSeeded_WithTheStableIds()
    {
        await using var context = postgres.NewContext();
        var rows = new List<(int Id, string Code)>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"ID\", \"CODE\" FROM \"CUST\".\"NTFM_STS_LKP\" ORDER BY \"ID\"";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt16(0), reader.GetString(1)));
        }

        Assert.Equal([(1, "OPEN"), (2, "NTFD"), (3, "EXPD")], rows);
    }
}
