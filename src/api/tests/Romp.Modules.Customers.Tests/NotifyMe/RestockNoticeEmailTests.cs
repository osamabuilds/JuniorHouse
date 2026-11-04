using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N6 (NTM-5, NTM-8, NTM-9): the restock email, its content and what happens when sending fails.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class RestockNoticeEmailTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    /// <summary>Creates a request, restocks the variant, and returns the queued restock message for that request.</summary>
    private static async Task<(NotifyRequest Request, Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage Message)> RestockedAsync(
        NewsletterHarness harness, long variant, string email)
    {
        await harness.Send(new CreateNotifyRequestCommand(variant, email));
        await harness.Send(new ProcessStockChangeCommand(variant, 6));
        var request = Assert.Single(await harness.NotifyRequestsAsync(variant), r => r.Email == email);
        var message = (await harness.EmailEventsAsync())
            .Single(m => System.Text.Json.JsonSerializer.Deserialize<CustomerEmailRequestedEvent>(m.Payload) is { Kind: CustomerEmailKind.RestockNotice } e && e.RelatedId == request.Id);
        return (request, message);
    }

    [Fact]
    [Trait("Spec", "NTM-5")]
    public async Task Delivering_SendsOneEmail_NamingTheProductAndSize_WithALinkToThePage()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, available: false, product: "Floral Summer Dress", size: "4-5 years", colour: "Navy");
        var email = NewEmail();
        var (request, message) = await RestockedAsync(harness, variant, email);
        harness.Catalog.PublishVariant(variant, available: true, product: "Floral Summer Dress", size: "4-5 years", colour: "Navy");

        await harness.DeliverAsync(message);

        var sent = Assert.Single(harness.Emails.Sent, m => m.To == email);
        Assert.Contains("Floral Summer Dress", sent.Subject, StringComparison.Ordinal);
        Assert.Contains("4-5 years", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains("Navy", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains($"https://shop.example.test/girls/dresses/product-{variant}", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains($"https://shop.example.test/girls/dresses/product-{variant}", sent.HtmlBody, StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(await harness.NotifyRequestsAsync(variant), r => r.Id == request.Id).SentAt);
    }

    [Fact]
    [Trait("Spec", "NTM-5")]
    public async Task TheEmail_HasNoMarketing_AndNoUnsubscribeMachinery()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        var email = NewEmail();
        var (_, message) = await RestockedAsync(harness, variant, email);

        await harness.DeliverAsync(message);

        var sent = Assert.Single(harness.Emails.Sent, m => m.To == email);
        Assert.Null(sent.Headers);
        Assert.DoesNotContain("newsletter", sent.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only email", sent.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "NTM-5")]
    public async Task AProductName_WithMarkup_IsEncodedInTheHtml()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, product: "<script>alert(1)</script> Dress");
        var email = NewEmail();
        var (_, message) = await RestockedAsync(harness, variant, email);

        await harness.DeliverAsync(message);

        var sent = Assert.Single(harness.Emails.Sent, m => m.To == email);
        Assert.DoesNotContain("<script>", sent.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", sent.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "NTM-8")]
    public async Task ARedeliveredMessage_SendsOnce()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        var email = NewEmail();
        var (_, message) = await RestockedAsync(harness, variant, email);

        await harness.DeliverAsync(message);
        await harness.DeliverAsync(message);
        // A different message for the same request (a duplicate event) is stopped by the sent time, not only by the inbox.
        await harness.DeliverAsync(new Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage { Id = message.Id + 1_000_000, Payload = message.Payload, EventType = message.EventType });

        Assert.Single(harness.Emails.Sent, m => m.To == email);
    }

    [Fact]
    [Trait("Spec", "NTM-9")]
    public async Task AVariantThatSoldOutAgain_StillGetsTheEmailOnce()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, available: false);
        var email = NewEmail();
        var (_, message) = await RestockedAsync(harness, variant, email);

        await harness.DeliverAsync(message);

        Assert.Single(harness.Emails.Sent, m => m.To == email);
    }

    [Fact]
    [Trait("Spec", "NTM-5")]
    public async Task AProductThatIsNoLongerPublished_SendsNothing_AndDoesNotFail()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        var email = NewEmail();
        var (_, message) = await RestockedAsync(harness, variant, email);
        harness.Catalog.UnpublishVariant(variant);

        await harness.DeliverAsync(message);

        Assert.DoesNotContain(harness.Emails.Sent, m => m.To == email);
    }

    [Fact]
    [Trait("Spec", "NTM-8")]
    public async Task AMissingRequest_IsIgnored()
    {
        using var harness = new NewsletterHarness(postgres);
        var payload = System.Text.Json.JsonSerializer.Serialize(new CustomerEmailRequestedEvent(CustomerEmailKind.RestockNotice, 987_654_321));

        await harness.DeliverAsync(new Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage { Id = Random.Shared.NextInt64(1_000_000_000, long.MaxValue / 2), Payload = payload, EventType = nameof(CustomerEmailRequestedEvent) });

        Assert.Empty(harness.Emails.Sent);
    }
}
