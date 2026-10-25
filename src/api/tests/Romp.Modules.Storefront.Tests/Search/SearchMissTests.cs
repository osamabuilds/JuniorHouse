using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Domain.Search;
using Romp.Modules.Storefront.Infrastructure.Search;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 7: the zero-result log, its redaction, its upsert and its purge (SRC-13).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchMissTests(StorefrontPostgres postgres)
{
    private static Task<List<SearchMiss>> RowsAsync(StorefrontHost host) =>
        host.QueryAsync(db => db.SearchMisses.AsNoTracking().OrderBy(m => m.QueryText).ToListAsync());

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Record_StoresTheNormalisedText_WithACount()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var stored = await host.SendAsync(new RecordMissCommand("  Dungree   SET "));

        Assert.True(stored);
        var row = Assert.Single(await RowsAsync(host));
        Assert.Equal(("dungree set", 1), (row.QueryText, row.MissCount));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), row.MissDate);
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Record_SameTextSameDay_AddsToOneRow()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.SendAsync(new RecordMissCommand("dungree"));
        await host.SendAsync(new RecordMissCommand("DUNGREE"));
        await host.SendAsync(new RecordMissCommand("dungree "));

        var row = Assert.Single(await RowsAsync(host));
        Assert.Equal(3, row.MissCount);
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Record_AtTheSameTime_LosesNoCount()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => host.SendAsync(new RecordMissCommand("frock"))));

        Assert.Equal(20, Assert.Single(await RowsAsync(host)).MissCount);
    }

    [Theory]
    [Trait("Spec", "SRC-13")]
    [InlineData("someone@example.com")]
    [InlineData("0300 1234567")]
    [InlineData("https://example.com/x")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Record_TextThatLooksPersonal_IsNotStored(string? text)
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var stored = await host.SendAsync(new RecordMissCommand(text));

        Assert.False(stored);
        Assert.Empty(await RowsAsync(host));
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Record_LongText_IsCutAtTheLimit()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.SendAsync(new RecordMissCommand(new string('a', 5000)));

        Assert.Equal(SearchQuery.MaxLength, Assert.Single(await RowsAsync(host)).QueryText.Length);
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Purge_RemovesOnlyDaysPastRetention()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new RecordMissCommand("fresh"));
        await host.QueryAsync(db => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "STFR"."SRCH_MISS" ("QRY_TEXT", "MISS_DT", "MISS_CNT", "INSR_DTE", "INSR_BY")
            VALUES ('old', current_date - 181, 4, now(), 'test'), ('edge', current_date - 180, 2, now(), 'test')
            """));

        var removed = await host.SendAsync(new PurgeMissesCommand());

        Assert.Equal(1, removed);
        Assert.Equal(["edge", "fresh"], (await RowsAsync(host)).Select(m => m.QueryText));
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Worker_RecordsOfferedTexts_AndSurvivesAFailingWrite()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var worker = host.Services.GetRequiredService<SearchMissWorker>();
        await worker.StartAsync(default);
        try
        {
            worker.Offer("someone@example.com"); // dropped by redaction
            worker.Offer("dungree");
            for (var i = 0; i < 50 && (await RowsAsync(host)).Count == 0; i++)
            {
                await Task.Delay(100);
            }
        }
        finally
        {
            await worker.StopAsync(default);
        }

        Assert.Equal(["dungree"], (await RowsAsync(host)).Select(m => m.QueryText));
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public void Offer_NeverBlocksOrThrows_WhenTheQueueIsFull()
    {
        var worker = new SearchMissWorker(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<SearchMissWorker>.Instance);

        for (var i = 0; i < 5000; i++)
        {
            worker.Offer("text " + i);
        }

        worker.Offer(null);
        worker.Offer("   ");
    }
}
