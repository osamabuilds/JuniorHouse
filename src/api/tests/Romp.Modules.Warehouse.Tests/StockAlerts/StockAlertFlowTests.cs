using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.StockAlerts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockAlerts;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Infrastructure.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.StockAlerts;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class StockAlertFlowTests(WarehousePostgres postgres)
{
    private const string Person = "Ayesha Khan";

    private static StockPost Move(long variant, long bin, int qty) =>
        new(variant, Grades.Accepted, bin, qty, qty > 0 ? MovementKinds.Putaway : MovementKinds.Adjustment, 77);

    /// <summary>Changes stock, then plays the part of the outbox consumer that reacts to the module's own stock event (ALR-9: a separate step).</summary>
    private static async Task ChangeAsync(WarehouseHost host, (long RefId, long VariantId, string Gtin, string Sku) variant, long bin, int qty)
    {
        await host.SendAsync(new TestPostCommand([Move(variant.RefId, bin, qty)]));
        await host.SendAsync(new EvaluateStockAlertCommand(variant.VariantId));
    }

    private static async Task<IReadOnlyList<AlertRowDto>> OpenAlertsAsync(WarehouseHost host, string sku, bool includeClosed = false) =>
        (await host.SendAsync(new SearchAlertsQuery(IncludeClosed: includeClosed, PageSize: 100))).Items.Where(a => a.SkuCode == sku).ToList();

    private static int AlertEvents(WhseDbContext db, string sku) =>
        db.Set<OutboxMessage>().Where(m => m.EventType == "StockAlertRaisedEvent").AsEnumerable().Count(m => m.Payload.Contains(sku, StringComparison.Ordinal));

    [Fact]
    [Trait("Spec", "ALR-1")]
    [Trait("Spec", "TC-STK-02")]
    public async Task CrossingTheThreshold_RaisesExactlyOneLowAlert_AndFurtherMovementsRaiseNone()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 20);
        Assert.Empty(await OpenAlertsAsync(host, variant.Sku));

        await ChangeAsync(host, variant, bin.Id, -15); // 5 = at the default threshold
        await ChangeAsync(host, variant, bin.Id, -1);
        await ChangeAsync(host, variant, bin.Id, -1);

        var alert = Assert.Single(await OpenAlertsAsync(host, variant.Sku));
        Assert.Equal(("LOW", 3, 5, false), (alert.LevelCode, alert.Available, alert.Threshold, alert.Acknowledged));
        await using var db = postgres.NewContext();
        Assert.Equal(1, AlertEvents(db, variant.Sku));
    }

    [Fact]
    [Trait("Spec", "ALR-2")]
    [Trait("Spec", "TC-STK-03")]
    public async Task ReachingZero_EscalatesTheSameAlertToStockOut_AndTheStockEventCarriesZeroAvailable()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 4);
        var low = Assert.Single(await OpenAlertsAsync(host, variant.Sku));

        await ChangeAsync(host, variant, bin.Id, -4);

        var alert = Assert.Single(await OpenAlertsAsync(host, variant.Sku));
        Assert.Equal((low.Id, "OUT", 0), (alert.Id, alert.LevelCode, alert.Available));
        await using var db = postgres.NewContext();
        Assert.Contains("\"Available\": 0", db.Set<OutboxMessage>().OrderBy(m => m.Id).AsEnumerable()
            .Last(m => m.EventType == "StockLevelChangedEvent" && m.Payload.Contains(variant.Sku, StringComparison.Ordinal)).Payload, StringComparison.Ordinal);
        Assert.Equal(2, AlertEvents(db, variant.Sku)); // opened Low, then raised again as Stock-out
    }

    [Fact]
    [Trait("Spec", "ALR-3")]
    [Trait("Spec", "TC-STK-08")]
    public async Task Restocking_AboveTheThreshold_ClosesTheAlert_ByItself()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 3);
        Assert.Single(await OpenAlertsAsync(host, variant.Sku));

        await ChangeAsync(host, variant, bin.Id, 20);

        Assert.Empty(await OpenAlertsAsync(host, variant.Sku));
        var history = Assert.Single(await OpenAlertsAsync(host, variant.Sku, includeClosed: true));
        Assert.Equal(23, history.Available); // a closed alert shows the quantity it closed at
    }

    [Fact]
    [Trait("Spec", "ALR-4")]
    public async Task AVariantThatNeverHeldStock_RaisesNoAlert()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();

        await host.SendAsync(new EvaluateStockAlertCommand(variant.VariantId));

        Assert.Empty(await OpenAlertsAsync(host, variant.Sku, includeClosed: true));
    }

    [Fact]
    [Trait("Spec", "ALR-3")]
    public async Task ReplayingTheSameEvent_CreatesNothingNew_AndTheDatabaseAllowsOnlyOneOpenAlert()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 2);

        await host.SendAsync(new EvaluateStockAlertCommand(variant.VariantId));
        await host.SendAsync(new EvaluateStockAlertCommand(variant.VariantId));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => host.SendAsync(new EvaluateStockAlertCommand(variant.VariantId))
            .ContinueWith(t => t.Exception, TaskScheduler.Default)));

        Assert.Single(await OpenAlertsAsync(host, variant.Sku, includeClosed: true));
    }

    [Fact]
    [Trait("Spec", "ALR-5")]
    public async Task Acknowledging_RecordsWhoAndWhen_DropsItFromTheUnacknowledgedCount_AndKeepsItOpen()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 2);
        var before = await host.SendAsync(new CountOpenAlertsQuery());
        var alert = Assert.Single(await OpenAlertsAsync(host, variant.Sku));

        var acknowledged = await host.SendAsync(new AcknowledgeAlertCommand(alert.Id, Person));

        var after = await host.SendAsync(new CountOpenAlertsQuery());
        Assert.True(acknowledged.Acknowledged);
        Assert.Equal(Person, acknowledged.AcknowledgedBy);
        Assert.Equal(before.Open, after.Open);
        Assert.Equal(before.Unacknowledged - 1, after.Unacknowledged);
        Assert.Single(await OpenAlertsAsync(host, variant.Sku));
        var unacknowledgedOnly = await host.SendAsync(new SearchAlertsQuery(Acknowledged: false, PageSize: 100));
        Assert.DoesNotContain(unacknowledgedOnly.Items, a => a.SkuCode == variant.Sku);
    }

    [Fact]
    [Trait("Spec", "ALR-5")]
    public async Task AnUnknownOrClosedAlert_CannotBeAcknowledged_AndABadPersonIsRefused()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 2);
        var alert = Assert.Single(await OpenAlertsAsync(host, variant.Sku));
        await ChangeAsync(host, variant, bin.Id, 30);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new AcknowledgeAlertCommand(999_999_999, Person)));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new AcknowledgeAlertCommand(alert.Id, Person)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new AcknowledgeAlertCommand(alert.Id, "x")));
    }

    [Fact]
    [Trait("Spec", "ALR-6")]
    public async Task ChangingTheThreshold_IsValidated_Audited_AndReevaluatesTheAlertAtOnce()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await ChangeAsync(host, variant, bin.Id, 8);
        Assert.Empty(await OpenAlertsAsync(host, variant.Sku));

        var row = await host.SendAsync(new SetThresholdCommand(variant.RefId, 10, Person));
        Assert.Equal((10, true, 8), (row.EffectiveThreshold, row.IsOverride, row.Available));
        Assert.Single(await OpenAlertsAsync(host, variant.Sku));

        await host.SendAsync(new SetThresholdCommand(variant.RefId, 2, Person));
        Assert.Empty(await OpenAlertsAsync(host, variant.Sku));

        await using var db = postgres.NewContext();
        var stored = await db.Thresholds.AsNoTracking().SingleAsync(t => t.VariantRefId == variant.RefId);
        Assert.Equal(2, stored.Quantity);
        Assert.NotEqual(default, stored.InsrDte);
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SetThresholdCommand(variant.RefId, -1, Person)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SetThresholdCommand(variant.RefId, 100_001, Person)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new SetThresholdCommand(987_654_321, 3, Person)));
    }

    [Fact]
    [Trait("Spec", "ALR-6")]
    public async Task TheConfiguredDefault_AppliesUntilAVariantHasItsOwn()
    {
        using var host = new WarehouseHost(postgres, o => o.DefaultLowStockThreshold = 12);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await ChangeAsync(host, variant, bin.Id, 12);

        Assert.Equal(12, Assert.Single(await OpenAlertsAsync(host, variant.Sku)).Threshold);
        var row = (await host.SendAsync(new SearchThresholdsQuery(variant.Sku))).Items.Single();
        Assert.Equal((12, false), (row.EffectiveThreshold, row.IsOverride));
    }

    [Fact]
    [Trait("Spec", "ALR-7")]
    public async Task TheAlertEvent_IsWrittenInTheSameTransactionAsTheAlert()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await ChangeAsync(host, variant, bin.Id, 1);

        await using var db = postgres.NewContext();
        var message = Assert.Single(db.Set<OutboxMessage>().Where(m => m.EventType == "StockAlertRaisedEvent").AsEnumerable().ToList(),
            m => m.Payload.Contains(variant.Sku, StringComparison.Ordinal));
        Assert.Contains("\"LevelCode\": \"LOW\"", message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ALR-9")]
    [Trait("Spec", "NFR-FT-08")]
    public void TheStockPostingPath_DoesNotDependOnAlerts_SoAnAlertFailureCanNeverRollBackAStockChange()
    {
        var dependencies = typeof(StockPosting).GetConstructors().Single().GetParameters().Select(p => p.ParameterType.Name).ToList();

        Assert.DoesNotContain(dependencies, n => n.Contains("Alert", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(StockPosting).Assembly.GetTypes().Where(t => t.Name == "StockPosting").SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)),
            f => f.FieldType.Name.Contains("Alert", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "ALR-8")]
    public async Task TheAlertAndThresholdLists_ArePaged_Filterable_AndRunAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new WarehouseHost(postgres, counter: counter);
        var bin = await host.NewBinAsync();
        var first = await host.NewVariantAsync();
        await ChangeAsync(host, first, bin.Id, 2);

        counter.Reset();
        await host.SendAsync(new SearchAlertsQuery(PageSize: 100));
        await host.SendAsync(new SearchThresholdsQuery(null, 1, 100));
        var small = counter.Count;

        for (var i = 0; i < 6; i++)
        {
            var v = await host.NewVariantAsync();
            await ChangeAsync(host, v, bin.Id, i % 2 == 0 ? 2 : 0 + 1);
        }

        counter.Reset();
        var alerts = await host.SendAsync(new SearchAlertsQuery(PageSize: 100));
        var thresholds = await host.SendAsync(new SearchThresholdsQuery(null, 1, 100));

        Assert.Equal(small, counter.Count);
        Assert.True(alerts.Items.Count >= 7);
        Assert.True(alerts.Items.Zip(alerts.Items.Skip(1)).All(p => p.First.Id > p.Second.Id));
        Assert.True(thresholds.Items.Count >= 7);
        Assert.All((await host.SendAsync(new SearchAlertsQuery(LevelId: AlertLevels.Low, PageSize: 100))).Items, a => Assert.Equal("LOW", a.LevelCode));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SearchAlertsQuery(LevelId: 9)));
        Assert.Equal(100, (await host.SendAsync(new SearchAlertsQuery(PageSize: 10_000))).PageSize);
    }
}
