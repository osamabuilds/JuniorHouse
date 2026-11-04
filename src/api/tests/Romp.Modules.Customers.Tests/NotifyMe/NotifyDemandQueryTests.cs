using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Infrastructure.Persistence;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N8 (NTM-11, NTM-12, NTM-13): the admin demand list, counts only, in a constant number of queries.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class NotifyDemandQueryTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    private sealed class Counter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static async Task WaitAsync(NewsletterHarness harness, long variant, int people)
    {
        harness.Catalog.PublishVariant(variant, product: $"Product {variant}", size: "4-5 years", colour: "Navy");
        for (var i = 0; i < people; i++)
        {
            await harness.Send(new Romp.Modules.Customers.Application.NotifyMe.CreateNotifyRequestCommand(variant, NewEmail()));
        }
    }

    private CustDbContext Counted(Counter counter) =>
        new(new DbContextOptionsBuilder<CustDbContext>()
            .UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CUST"))
            .AddInterceptors(counter)
            .Options);

    [Fact]
    [Trait("Spec", "NTM-11")]
    public async Task TheList_ShowsVariantsWithOpenRequests_MostWaitingFirst_WithTheirNames()
    {
        using var harness = new NewsletterHarness(postgres);
        var few = NewsletterHarness.NewVariantId();
        var many = few + 1;
        await WaitAsync(harness, few, 1);
        await WaitAsync(harness, many, 3);

        // A closed request must not count.
        await harness.Send(new Romp.Modules.Customers.Application.NotifyMe.ProcessStockChangeCommand(few, 5));

        var page = await harness.Send(new ListNotifyDemandQuery(1, 100));

        var item = Assert.Single(page.Items, i => i.VariantId == many);
        Assert.Equal((3, $"Product {many}", "Navy", "4-5 years"), (item.Waiting, item.ProductName, item.ColourName, item.SizeName));
        Assert.DoesNotContain(page.Items, i => i.VariantId == few);
        var counts = page.Items.Select(i => i.Waiting).ToList();
        Assert.Equal(counts.OrderByDescending(c => c), counts);
    }

    [Fact]
    [Trait("Spec", "NTM-12")]
    public async Task TheList_NeverCarriesAnAddress()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitAsync(harness, variant, 2);

        var page = await harness.Send(new ListNotifyDemandQuery(1, 100));

        var json = System.Text.Json.JsonSerializer.Serialize(page);
        Assert.DoesNotContain("@", json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", json, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(NotifyDemandItem).GetProperties(), p => p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Spec", "NTM-13")]
    public async Task APage_IsOneGroupedQueryPlusOneCountAndOneBulkCatalogCall()
    {
        using var harness = new NewsletterHarness(postgres);
        var first = NewsletterHarness.NewVariantId();
        for (var i = 0; i < 6; i++)
        {
            await WaitAsync(harness, first + i, 1);
        }

        var counter = new Counter();
        await using var db = Counted(counter);
        var before = harness.Catalog.VariantCalls;
        var handler = new ListNotifyDemandQueryHandler(db, harness.Catalog);

        var page = await handler.Handle(new ListNotifyDemandQuery(1, 100), CancellationToken.None);

        Assert.True(page.Items.Count >= 6);
        Assert.Equal(2, counter.Count);
        Assert.Equal(before + 1, harness.Catalog.VariantCalls);
    }

    [Theory]
    [Trait("Spec", "NTM-13")]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 5000, 1, 100)]
    public async Task PagingIsClamped(int page, int pageSize, int expectedPage, int expectedSize)
    {
        using var harness = new NewsletterHarness(postgres);

        var result = await harness.Send(new ListNotifyDemandQuery(page, pageSize));

        Assert.Equal((expectedPage, expectedSize), (result.Page, result.PageSize));
    }

    [Fact]
    [Trait("Spec", "NTM-11")]
    public async Task AVariantNoLongerPublished_StillListsItsCount_WithAPlaceholderName()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitAsync(harness, variant, 2);
        harness.Catalog.UnpublishVariant(variant);

        var page = await harness.Send(new ListNotifyDemandQuery(1, 100));

        var item = Assert.Single(page.Items, i => i.VariantId == variant);
        Assert.Equal(2, item.Waiting);
        Assert.Equal("(no longer published)", item.ProductName);
    }

    [Fact]
    [Trait("Spec", "NTM-11")]
    public async Task TheOldestRequestDate_IsReported()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitAsync(harness, variant, 2);
        var rows = await harness.NotifyRequestsAsync(variant);

        var page = await harness.Send(new ListNotifyDemandQuery(1, 100));

        var item = Assert.Single(page.Items, i => i.VariantId == variant);
        Assert.Equal(rows.Min(r => r.InsrDte), item.OldestRequestAt);
        Assert.Equal(NotifyStatus.Open, rows[0].Status);
    }
}
