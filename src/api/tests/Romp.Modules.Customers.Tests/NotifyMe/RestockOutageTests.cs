using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N6 (NTM-8, NTM-9, NFR-FT-08): an email outage never touches the stock change or the request, and the mail is retried.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class RestockOutageTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    [Fact]
    [Trait("Spec", "NTM-8")]
    public async Task WhenSendingFails_TheRequestStaysNotified_ThenTheRetrySendsItOnce()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        var email = NewEmail();
        await harness.Send(new CreateNotifyRequestCommand(variant, email));
        await harness.Send(new ProcessStockChangeCommand(variant, 6));
        var request = Assert.Single(await harness.NotifyRequestsAsync(variant));
        var message = (await harness.EmailEventsAsync())
            .Single(m => System.Text.Json.JsonSerializer.Deserialize<CustomerEmailRequestedEvent>(m.Payload) is { Kind: CustomerEmailKind.RestockNotice } e && e.RelatedId == request.Id);

        harness.Emails.Down = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DeliverAsync(message));

        var during = Assert.Single(await harness.NotifyRequestsAsync(variant));
        Assert.Equal((NotifyStatus.Notified, (DateTimeOffset?)null), (during.Status, during.SentAt));
        Assert.DoesNotContain(harness.Emails.Sent, m => m.To == email);

        harness.Emails.Down = false;
        await harness.DeliverAsync(message);
        await harness.DeliverAsync(message);

        Assert.Single(harness.Emails.Sent, m => m.To == email);
        Assert.NotNull(Assert.Single(await harness.NotifyRequestsAsync(variant)).SentAt);
    }

    [Fact]
    [Trait("Spec", "NTM-8")]
    public async Task TheStockChange_DoesNotSendAnything_ItOnlyQueues()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        await harness.Send(new CreateNotifyRequestCommand(variant, NewEmail()));
        harness.Emails.Down = true;

        var notified = await harness.Send(new ProcessStockChangeCommand(variant, 6));

        Assert.Equal(1, notified);
        Assert.Equal(NotifyStatus.Notified, Assert.Single(await harness.NotifyRequestsAsync(variant)).Status);
    }
}
