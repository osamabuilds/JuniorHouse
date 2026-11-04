using Romp.Modules.Customers.Application.Newsletter;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Newsletter;

/// <summary>customers/newsletter NL3 and NL4 (NLS-3, NLS-6, ADR 0022): the welcome email through the outbox event and its consumer.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class NewsletterWelcomeEmailTests(CustomersPostgres postgres)
{
    private static async Task<(long Id, Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage Message)> SubscribeAsync(NewsletterHarness harness, string email)
    {
        var before = (await harness.EmailEventsAsync()).Select(m => m.Id).ToHashSet();
        await harness.Send(new SubscribeNewsletterCommand(email));
        var id = Assert.Single(await harness.StoredAsync(email)).Id;
        var message = Assert.Single(await harness.EmailEventsAsync(), m => !before.Contains(m.Id));
        return (id, message);
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task Subscribing_QueuesOneWelcomeEvent_ThatNamesTheRecordAndNeverTheAddress()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();

        var (id, message) = await SubscribeAsync(harness, email);

        Assert.DoesNotContain(email, message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", message.Payload, StringComparison.Ordinal);
        var payload = System.Text.Json.JsonSerializer.Deserialize<CustomerEmailRequestedEvent>(message.Payload)!;
        Assert.Equal((CustomerEmailKind.NewsletterWelcome, id), (payload.Kind, payload.RelatedId));
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task Delivering_SendsTheWelcome_WithTheUnsubscribeLinkAndHeaders()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var (id, message) = await SubscribeAsync(harness, email);

        await harness.DeliverAsync(message);

        var sent = Assert.Single(harness.Emails.Sent, m => m.To == email);
        var token = UnsubscribeToken.Create(id, NewsletterHarness.Key);
        Assert.Contains($"https://shop.example.test/newsletter/unsubscribe?token={Uri.EscapeDataString(token)}", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains("Unsubscribe", sent.HtmlBody, StringComparison.Ordinal);
        Assert.NotNull(sent.Headers);
        Assert.StartsWith("<https://shop.example.test/customer-api/newsletter/unsubscribe?token=", sent.Headers["List-Unsubscribe"], StringComparison.Ordinal);
        Assert.Equal("List-Unsubscribe=One-Click", sent.Headers["List-Unsubscribe-Post"]);
        Assert.DoesNotContain("\n", sent.Headers["List-Unsubscribe"], StringComparison.Ordinal);
        Assert.DoesNotContain(email, sent.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(Assert.Single(await harness.StoredAsync(email)).WelcomeSentAt);
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task Delivering_TheSameMessageTwice_SendsOneWelcome()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var (_, message) = await SubscribeAsync(harness, email);

        await harness.DeliverAsync(message);
        await harness.DeliverAsync(message);

        Assert.Single(harness.Emails.Sent, m => m.To == email);
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task ASecondSubscribe_QueuesNoSecondWelcome()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var (_, message) = await SubscribeAsync(harness, email);
        await harness.DeliverAsync(message);
        var events = (await harness.EmailEventsAsync()).Count;

        await harness.Send(new SubscribeNewsletterCommand(email));

        Assert.Equal(events, (await harness.EmailEventsAsync()).Count);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task AnAddress_ThatUnsubscribedBeforeTheSend_GetsNothing()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var (id, message) = await SubscribeAsync(harness, email);
        await harness.Send(new UnsubscribeByTokenCommand(UnsubscribeToken.Create(id, NewsletterHarness.Key)));

        await harness.DeliverAsync(message);

        Assert.DoesNotContain(harness.Emails.Sent, m => m.To == email);
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task ResubscribingAfterAnUnsubscribe_SendsAFreshWelcome()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var (id, first) = await SubscribeAsync(harness, email);
        await harness.DeliverAsync(first);
        await harness.Send(new UnsubscribeByTokenCommand(UnsubscribeToken.Create(id, NewsletterHarness.Key)));

        var (_, second) = await SubscribeAsync(harness, email);
        await harness.DeliverAsync(second);

        Assert.Equal(2, harness.Emails.Sent.Count(m => m.To == email));
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task AnEmailOutage_FailsTheDelivery_ThenARetryDeliversOnce()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var (_, message) = await SubscribeAsync(harness, email);

        harness.Emails.Down = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DeliverAsync(message));
        Assert.Null(Assert.Single(await harness.StoredAsync(email)).WelcomeSentAt);
        Assert.DoesNotContain(harness.Emails.Sent, m => m.To == email);

        harness.Emails.Down = false;
        await harness.DeliverAsync(message);
        await harness.DeliverAsync(message);

        Assert.Single(harness.Emails.Sent, m => m.To == email);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task AKindWithNoHandlerYet_IsAnErrorNotASilentSkip()
    {
        using var harness = new NewsletterHarness(postgres);
        var (_, message) = await SubscribeAsync(harness, NewsletterHarness.NewEmail());
        var restock = new Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage
        {
            Id = message.Id,
            EventType = nameof(CustomerEmailRequestedEvent),
            Payload = System.Text.Json.JsonSerializer.Serialize(new CustomerEmailRequestedEvent(CustomerEmailKind.RestockNotice, 1)),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DeliverAsync(restock));
    }
}
