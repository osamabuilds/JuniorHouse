using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S3 (ACT-10, ACT-11, ACT-27, ACT-29): the sign-in tables on a real PostgreSQL.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class CustomerSignInMigrationTests(CustomersPostgres postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private async Task<Customer> NewCustomerAsync(string? mobile = "0300 1234567")
    {
        await using var context = postgres.NewContext();
        var customer = Customer.Register($"signin.{Guid.NewGuid():N}@example.test", "Sign", "In", mobile, "hash-1");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    [Fact]
    [Trait("Spec", "ACT-29")]
    public async Task Customer_StoresThePasswordHashTheVerifiedDateAndTheMobile()
    {
        var created = await NewCustomerAsync();

        await using var context = postgres.NewContext();
        var customer = await context.Customers.SingleAsync(c => c.Id == created.Id);
        customer.MarkEmailVerified(customer.Email, Now);
        customer.RecordSignIn(Now);
        await context.SaveChangesAsync();

        await using var reread = postgres.NewContext();
        var stored = await reread.Customers.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("hash-1", stored.PasswordHash);
        Assert.Equal(Now, stored.EmailVerifiedAt);
        Assert.Equal(Now, stored.LastSignedInAt);
        Assert.Equal("+923001234567", stored.Phone);
    }

    [Fact]
    [Trait("Spec", "ACT-29")]
    public async Task TwoCustomers_MayShareAnUnverifiedMobile()
    {
        await NewCustomerAsync("0311 7654321");
        await NewCustomerAsync("0311 7654321");

        await using var context = postgres.NewContext();
        Assert.Equal(2, await context.Customers.CountAsync(c => c.Phone == "+923117654321"));
    }

    [Fact]
    [Trait("Spec", "ACT-3")]
    public async Task TheEmailStaysUnique()
    {
        var first = await NewCustomerAsync();
        await using var context = postgres.NewContext();
        context.Customers.Add(Customer.Register(first.Email, "Dup", "Licate", "0300 1234567", "hash"));

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)failure.InnerException!).SqlState);
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    public async Task Session_StoresOnlyHashes_AndEachHashIsUnique()
    {
        var customer = await NewCustomerAsync();
        var (session, tokens) = CustomerSession.Start(customer.Id, Now, CustomerSessionLifetimes.KeepSignedIn, "203.0.113.9", "test-agent");
        await using (var context = postgres.NewContext())
        {
            context.Sessions.Add(session);
            await context.SaveChangesAsync();
        }

        await using var reread = postgres.NewContext();
        var stored = await reread.Sessions.SingleAsync(s => s.Id == session.Id);
        Assert.True(stored.AccessTokenMatches(tokens.Access.Reveal()));
        Assert.True(stored.KeepSignedIn);
        Assert.Equal(Now.AddDays(30), stored.AbsoluteExpiresAt);

        await using var duplicate = postgres.NewContext();
        var (other, _) = CustomerSession.Start(customer.Id, Now, CustomerSessionLifetimes.Standard);
        typeof(CustomerSession).GetProperty(nameof(CustomerSession.AccessTokenHash))!.SetValue(other, session.AccessTokenHash);
        duplicate.Sessions.Add(other);
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    [Trait("Spec", "ACT-13")]
    public async Task Session_RevokeReason_IsAKeyIntoTheLookup()
    {
        var customer = await NewCustomerAsync();
        var (session, _) = CustomerSession.Start(customer.Id, Now, CustomerSessionLifetimes.Standard);
        session.Revoke(CustomerSessionRevokeReason.PasswordChanged, Now.AddMinutes(1));
        await using var context = postgres.NewContext();
        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        var reasons = await context.Database
            .SqlQuery<string>($"""SELECT "CODE" AS "Value" FROM "CUST"."SESS_RVKD_RSN_LKP" WHERE "ID" = {(short)CustomerSessionRevokeReason.PasswordChanged}""")
            .ToListAsync();
        Assert.Equal(["PWD_CHNG"], reasons);
    }

    [Fact]
    [Trait("Spec", "ACT-8")]
    public async Task Tokens_AreStoredByHash_AndConsumedOnce()
    {
        var customer = await NewCustomerAsync();
        var (reset, resetSecret) = PasswordResetToken.Issue(customer.Id, Now);
        var (verify, _) = EmailVerificationToken.Issue(customer.Id, customer.Email, Now);
        await using (var context = postgres.NewContext())
        {
            context.PasswordResetTokens.Add(reset);
            context.EmailVerificationTokens.Add(verify);
            await context.SaveChangesAsync();
        }

        await using var again = postgres.NewContext();
        var stored = await again.PasswordResetTokens.SingleAsync(t => t.Id == reset.Id);
        Assert.True(stored.Consume(resetSecret.Reveal(), Now.AddMinutes(1)));
        await again.SaveChangesAsync();

        await using var last = postgres.NewContext();
        var used = await last.PasswordResetTokens.SingleAsync(t => t.Id == reset.Id);
        Assert.False(used.Consume(resetSecret.Reveal(), Now.AddMinutes(2)));
        Assert.Equal(customer.Email, (await last.EmailVerificationTokens.SingleAsync(t => t.Id == verify.Id)).Email);
    }

    [Fact]
    [Trait("Spec", "ACT-10")]
    public async Task LoginAttempt_IsStoredWithoutAnyPassword()
    {
        var attempt = LoginAttempt.Record("Someone@Example.com", "203.0.113.9", LoginOutcome.BadCredentials, Now);
        await using (var context = postgres.NewContext())
        {
            context.LoginAttempts.Add(attempt);
            await context.SaveChangesAsync();
        }

        await using var reread = postgres.NewContext();
        var stored = await reread.LoginAttempts.SingleAsync(a => a.Id == attempt.Id);
        Assert.Equal("Someone@Example.com", stored.AttemptedEmail);
        Assert.Equal(LoginOutcome.BadCredentials, stored.Outcome);

        var columns = await reread.Database
            .SqlQuery<string>($"""SELECT column_name AS "Value" FROM information_schema.columns WHERE table_schema = 'CUST' AND table_name = 'CUST_LGIN_ATMP'""")
            .ToListAsync();
        Assert.DoesNotContain(columns, c => c.Contains("PWD", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "ACT-10")]
    public async Task EveryEnumValue_HasALookupRow()
    {
        await using var context = postgres.NewContext();
        var outcomes = await context.Database.SqlQuery<short>($"""SELECT "ID" AS "Value" FROM "CUST"."LGIN_OTCM_LKP" ORDER BY "ID" """).ToListAsync();
        var reasons = await context.Database.SqlQuery<short>($"""SELECT "ID" AS "Value" FROM "CUST"."SESS_RVKD_RSN_LKP" ORDER BY "ID" """).ToListAsync();

        Assert.Equal(Enum.GetValues<LoginOutcome>().Select(v => (short)v).Order(), outcomes);
        Assert.Equal(Enum.GetValues<CustomerSessionRevokeReason>().Select(v => (short)v).Order(), reasons);
    }
}
