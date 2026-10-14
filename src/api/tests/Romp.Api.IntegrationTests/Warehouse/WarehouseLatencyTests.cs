using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;

namespace Romp.Api.IntegrationTests.Warehouse;

/// <summary>
/// NFR-SC-06 / SCRUM-120 (decision D8): a stock change reaches its consumers within 5 seconds at the 95th percentile. The time is the
/// wait for the next poll plus the delivery itself, so the test checks both: the configured poll interval, and the measured time from
/// the committed change to every handler having run, over repeated runs on the real stack.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class WarehouseLatencyTests(ProductionApiFixture api)
{
    private const string Person = "Ayesha Khan";

    private static long _seed = 800_000;

    private static int _zone;

    [Fact]
    [Trait("Spec", "LED-4")]
    [Trait("Spec", "NFR-SC-06")]
    public void ThePollInterval_LeavesRoomInsideTheFiveSecondBudget()
    {
        var options = api.Services.GetRequiredService<OutboxDispatcherOptions>();

        Assert.True(options.PollInterval <= TimeSpan.FromSeconds(2), $"poll interval is {options.PollInterval}; worst-case wait plus delivery must stay under 5 s");
    }

    [Fact]
    [Trait("Spec", "LED-4")]
    [Trait("Spec", "PUT-11")]
    [Trait("Spec", "NFR-SC-06")]
    public async Task AStockChange_IsDeliveredToItsConsumers_WithinFiveSecondsAtTheNinetyFifthPercentile_PlusTheWorstCasePollWait()
    {
        var n = Interlocked.Increment(ref _seed);
        var gtin = Gtin13.WithCheckDigit("6" + n.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture));
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordVariantAssignedCommand(n, 11_000 + n, 10, 1, $"ROMP-LAT-{n}", gtin));
        }

        var bin = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync("/api/warehouse/bins",
            new { zone = "L" + (Interlocked.Increment(ref _zone) % 256).ToString("X2", System.Globalization.CultureInfo.InvariantCulture), aisle = "01", shelf = "A", actingPerson = Person }));
        var binCode = bin.GetProperty("binCode").GetString();
        await api.DispatchAsync();

        var worstPoll = api.Services.GetRequiredService<OutboxDispatcherOptions>().PollInterval;
        var samples = new List<TimeSpan>();
        for (var i = 0; i < 30; i++)
        {
            var clock = Stopwatch.StartNew();
            var response = await api.PostAsync("/api/warehouse/adjustments", new
            {
                gtin, binCode, gradeId = 1, quantityChange = 1, reasonId = 4, reconfirmed = false, idempotencyKey = Guid.NewGuid(), actingPerson = Person,
            });
            Assert.True(response.IsSuccessStatusCode);
            await api.DispatchAsync();
            clock.Stop();
            samples.Add(clock.Elapsed + worstPoll); // commit to delivered, assuming the change landed just after a poll
        }

        var p95 = samples.Order().ElementAt((int)Math.Ceiling(samples.Count * 0.95) - 1);
        Assert.True(p95 < TimeSpan.FromSeconds(5), $"p95 commit-to-delivery was {p95.TotalMilliseconds:F0} ms (worst-case poll wait included)");
    }

    [Fact]
    [Trait("Spec", "PUT-17")]
    [Trait("Spec", "NFR-SC-01")]
    public async Task TheScanLookup_AnswersUnderThreeHundredMilliseconds_AtTheNinetyFifthPercentile_OverAThousandScans()
    {
        var n = Interlocked.Increment(ref _seed);
        var gtin = Gtin13.WithCheckDigit("7" + n.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture));
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordVariantAssignedCommand(n, 12_000 + n, 10, 1, $"ROMP-SCAN-{n}", gtin));
        }

        for (var warm = 0; warm < 20; warm++)
        {
            await api.Client.GetAsync($"/api/warehouse/putaway/scan/{gtin}");
        }

        var samples = new List<double>(1_000);
        for (var i = 0; i < 1_000; i++)
        {
            var clock = Stopwatch.StartNew();
            var response = await api.Client.GetAsync($"/api/warehouse/putaway/scan/{gtin}");
            clock.Stop();
            Assert.True(response.IsSuccessStatusCode);
            samples.Add(clock.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
        Assert.True(p95 < 300, $"p95 scan lookup was {p95:F1} ms");
    }
}
