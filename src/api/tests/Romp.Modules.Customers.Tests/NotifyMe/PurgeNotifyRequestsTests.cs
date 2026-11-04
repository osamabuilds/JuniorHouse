using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N7 (Q-NTM-2): open requests expire after 90 days, closed ones are deleted after 30.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class PurgeNotifyRequestsTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    private async Task<long> InsertAsync(NewsletterHarness harness, long variant, short status, int ageDays)
    {
        harness.Catalog.PublishVariant(variant);
        await using var db = postgres.NewContext();
        return (await db.Database.SqlQuery<long>($"""
            INSERT INTO "CUST"."NTFM_REQ" ("VRNT_ID", "EML", "STS_ID", "INSR_DTE", "INSR_BY", "UPDT_DTE")
            VALUES ({variant}, {NewEmail()}, {status}, now() - make_interval(days => {ageDays}), 'test', now() - make_interval(days => {ageDays}))
            RETURNING "ID" AS "Value"
            """).ToListAsync()).Single();
    }

    [Fact]
    [Trait("Spec", "NTM-12")]
    public async Task AnOpenRequest_PastNinetyDays_Expires_AndARecentOneStays()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        var old = await InsertAsync(harness, variant, 1, 91);
        var recent = await InsertAsync(harness, variant, 1, 10);

        var result = await harness.Send(new PurgeNotifyRequestsCommand());

        Assert.True(result.Expired >= 1);
        var rows = await harness.NotifyRequestsAsync(variant);
        Assert.Equal(NotifyStatus.Expired, rows.Single(r => r.Id == old).Status);
        Assert.Equal(NotifyStatus.Open, rows.Single(r => r.Id == recent).Status);
    }

    [Fact]
    [Trait("Spec", "NTM-12")]
    public async Task AClosedRequest_PastThirtyDays_IsDeleted_AndARecentOneStays()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        var oldNotified = await InsertAsync(harness, variant, 2, 31);
        var oldExpired = await InsertAsync(harness, variant, 3, 45);
        var recentNotified = await InsertAsync(harness, variant, 2, 5);

        var result = await harness.Send(new PurgeNotifyRequestsCommand());

        Assert.True(result.Deleted >= 2);
        Assert.Equal([recentNotified], (await harness.NotifyRequestsAsync(variant)).Select(r => r.Id));
        Assert.DoesNotContain(oldNotified, (await harness.NotifyRequestsAsync(variant)).Select(r => r.Id));
        Assert.DoesNotContain(oldExpired, (await harness.NotifyRequestsAsync(variant)).Select(r => r.Id));
    }

    [Fact]
    [Trait("Spec", "NTM-12")]
    public async Task AnExpiredRequest_IsNotDeletedTheSameDay_ItWaitsTheClosedPeriod()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        var old = await InsertAsync(harness, variant, 1, 120);

        await harness.Send(new PurgeNotifyRequestsCommand());

        Assert.Equal(NotifyStatus.Expired, Assert.Single(await harness.NotifyRequestsAsync(variant), r => r.Id == old).Status);
    }
}
