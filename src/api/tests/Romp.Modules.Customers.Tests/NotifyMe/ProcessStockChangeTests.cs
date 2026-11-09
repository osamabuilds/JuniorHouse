using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Infrastructure.NotifyMe;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N5 (NTM-4, NTM-8): a restock closes the open requests and queues one email event each, once.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class ProcessStockChangeTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    private static StockLevelChangedEvent Changed(long variantId, int available) =>
        new(variantId, 1, 1, 1, "SKU", "0000000000000", available, available, available, 0, "PUTAWAY");

    private static async Task WaitingAsync(NewsletterHarness harness, long variant, params string[] emails)
    {
        harness.Catalog.PublishVariant(variant, available: false);
        foreach (var email in emails)
        {
            await harness.Send(new CreateNotifyRequestCommand(variant, email));
        }
    }

    private static async Task<List<long>> RestockEventIdsAsync(NewsletterHarness harness) =>
        (await harness.EmailEventsAsync())
            .Select(m => System.Text.Json.JsonSerializer.Deserialize<CustomerEmailRequestedEvent>(m.Payload)!)
            .Where(e => e.Kind == CustomerEmailKind.RestockNotice)
            .Select(e => e.RelatedId)
            .ToList();

    [Fact]
    [Trait("Spec", "NTM-4")]
    public void TheMapper_TakesTheVariantAndTheAvailableQuantity() =>
        Assert.Equal(new ProcessStockChangeCommand(42, 9), StockChangedMapper.ToCommand(Changed(42, 9)));

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task ARestock_ClosesEveryOpenRequest_AndQueuesOneEmailEventEach()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitingAsync(harness, variant, NewEmail(), NewEmail(), NewEmail());
        var before = (await RestockEventIdsAsync(harness)).Count;

        var notified = await harness.Send(new ProcessStockChangeCommand(variant, 12));

        var rows = await harness.NotifyRequestsAsync(variant);
        Assert.Equal(3, notified);
        Assert.All(rows, r => Assert.Equal(NotifyStatus.Notified, r.Status));
        Assert.All(rows, r => Assert.NotNull(r.NotifiedAt));
        var queued = (await RestockEventIdsAsync(harness)).Skip(before).Order().ToList();
        Assert.Equal(rows.Select(r => r.Id).Order(), queued);
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task TheEmailEvent_NamesTheRequestAndNeverTheAddress()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        var email = NewEmail();
        await WaitingAsync(harness, variant, email);

        await harness.Send(new ProcessStockChangeCommand(variant, 3));

        var message = (await harness.EmailEventsAsync()).Last();
        Assert.DoesNotContain(email, message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", message.Payload, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "NTM-4")]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task AStockChangeToNothingAvailable_LeavesRequestsOpen(int available)
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitingAsync(harness, variant, NewEmail());

        var notified = await harness.Send(new ProcessStockChangeCommand(variant, available));

        Assert.Equal(0, notified);
        Assert.All(await harness.NotifyRequestsAsync(variant), r => Assert.Equal(NotifyStatus.Open, r.Status));
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task ARepeatedStockChange_NotifiesNobodyTwice()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitingAsync(harness, variant, NewEmail(), NewEmail());
        await harness.Send(new ProcessStockChangeCommand(variant, 5));
        var after = (await RestockEventIdsAsync(harness)).Count;

        var again = await harness.Send(new ProcessStockChangeCommand(variant, 8));

        Assert.Equal(0, again);
        Assert.Equal(after, (await RestockEventIdsAsync(harness)).Count);
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task OtherVariants_AreNotTouched()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        var other = variant + 1;
        await WaitingAsync(harness, variant, NewEmail());
        await WaitingAsync(harness, other, NewEmail());

        await harness.Send(new ProcessStockChangeCommand(variant, 5));

        Assert.All(await harness.NotifyRequestsAsync(other), r => Assert.Equal(NotifyStatus.Open, r.Status));
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task MoreThanOneBatch_AreAllNotifiedInOneRun()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitingAsync(harness, variant, Enumerable.Range(0, ProcessStockChangeCommandHandler.BatchSize + 20).Select(_ => NewEmail()).ToArray());

        var notified = await harness.Send(new ProcessStockChangeCommand(variant, 5));

        Assert.Equal(ProcessStockChangeCommandHandler.BatchSize + 20, notified);
        Assert.All(await harness.NotifyRequestsAsync(variant), r => Assert.Equal(NotifyStatus.Notified, r.Status));
    }

    [Fact]
    [Trait("Spec", "NTM-8")]
    public async Task ARedeliveredMessage_IsHandledOnce()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        await WaitingAsync(harness, variant, NewEmail());
        var messageId = Random.Shared.NextInt64(1_000_000_000, long.MaxValue / 2);

        await harness.DeliverStockChangeAsync(Changed(variant, 4), messageId);
        var queued = (await RestockEventIdsAsync(harness)).Count;
        // A second sold-out and restock between the two deliveries would add a request; redelivery must still do nothing.
        await harness.Send(new CreateNotifyRequestCommand(variant, NewEmail()));
        await harness.DeliverStockChangeAsync(Changed(variant, 4), messageId);

        Assert.Equal(queued, (await RestockEventIdsAsync(harness)).Count);
        Assert.Contains(await harness.NotifyRequestsAsync(variant), r => r.Status == NotifyStatus.Open);
    }
}
