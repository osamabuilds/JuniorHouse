using Romp.Modules.Customers.Application.Newsletter;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Newsletter;

/// <summary>customers/newsletter NL5 (NLS-1..5): subscribing, unsubscribing by link and the account toggle, through the real pipeline and database.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class NewsletterConsentTests(CustomersPostgres postgres)
{
    [Fact]
    [Trait("Spec", "NLS-1")]
    public async Task Subscribe_RecordsTheAddressOnce_WithTheFooterAsSource()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();

        await harness.Send(new SubscribeNewsletterCommand(email));

        var stored = Assert.Single(await harness.StoredAsync(email));
        Assert.Equal(NewsletterStatus.Subscribed, stored.Status);
        Assert.Equal([(ConsentAction.Subscribed, ConsentSource.Footer)], stored.History);
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public async Task Subscribe_TheSameAddressAgainInAnyCase_ChangesNothing()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        await harness.Send(new SubscribeNewsletterCommand(email));

        await harness.Send(new SubscribeNewsletterCommand("  " + email.ToUpperInvariant() + " "));

        var stored = Assert.Single(await harness.StoredAsync(email));
        Assert.Single(stored.History);
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public async Task Subscribe_TheSameAddressAtOnce_LeavesOneRecordAndQueuesOneWelcome()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var before = (await harness.EmailEventsAsync()).Count;

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => harness.Send(new SubscribeNewsletterCommand(email))));

        var stored = Assert.Single(await harness.StoredAsync(email));
        Assert.Single(stored.History);
        Assert.Equal(before + 1, (await harness.EmailEventsAsync()).Count);
    }

    [Theory]
    [Trait("Spec", "NLS-1")]
    [InlineData("not an email")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Subscribe_WithAnUnreadableAddress_IsRefused_AndStoresNothing(string? email)
    {
        using var harness = new NewsletterHarness(postgres);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => harness.Send(new SubscribeNewsletterCommand(email)));
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task Subscribe_WhileSignedIn_StillCountsAsTheFooter_NotAnotherAccount()
    {
        using var harness = new NewsletterHarness(postgres);
        var customer = await harness.NewCustomerAsync();
        var email = NewsletterHarness.NewEmail();

        await harness.Send(new SubscribeNewsletterCommand(email), customer);

        Assert.Equal([(ConsentAction.Subscribed, ConsentSource.Footer)], Assert.Single(await harness.StoredAsync(email)).History);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task Unsubscribe_ByItsOwnLink_WorksSignedOut_AndRepeatsHarmlessly()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        await harness.Send(new SubscribeNewsletterCommand(email));
        var id = Assert.Single(await harness.StoredAsync(email)).Id;
        var token = UnsubscribeToken.Create(id, NewsletterHarness.Key);

        Assert.True(await harness.Send(new UnsubscribeByTokenCommand(token)));
        Assert.True(await harness.Send(new UnsubscribeByTokenCommand(token)));

        var stored = Assert.Single(await harness.StoredAsync(email));
        Assert.Equal(NewsletterStatus.Unsubscribed, stored.Status);
        Assert.Equal([(ConsentAction.Subscribed, ConsentSource.Footer), (ConsentAction.Unsubscribed, ConsentSource.Link)], stored.History);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task Unsubscribe_ByALinkForAnotherRecord_LeavesThisOneSubscribed()
    {
        using var harness = new NewsletterHarness(postgres);
        var mine = NewsletterHarness.NewEmail();
        var theirs = NewsletterHarness.NewEmail();
        await harness.Send(new SubscribeNewsletterCommand(mine));
        await harness.Send(new SubscribeNewsletterCommand(theirs));
        var theirId = Assert.Single(await harness.StoredAsync(theirs)).Id;

        await harness.Send(new UnsubscribeByTokenCommand(UnsubscribeToken.Create(theirId, NewsletterHarness.Key)));

        Assert.Equal(NewsletterStatus.Subscribed, Assert.Single(await harness.StoredAsync(mine)).Status);
        Assert.Equal(NewsletterStatus.Unsubscribed, Assert.Single(await harness.StoredAsync(theirs)).Status);
    }

    [Theory]
    [Trait("Spec", "NLS-3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("MQ.not-a-signature")]
    public async Task Unsubscribe_ByAForgedOrBrokenLink_IsRefused_AndChangesNothing(string? token)
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        await harness.Send(new SubscribeNewsletterCommand(email));

        Assert.False(await harness.Send(new UnsubscribeByTokenCommand(token)));

        Assert.Equal(NewsletterStatus.Subscribed, Assert.Single(await harness.StoredAsync(email)).Status);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task Unsubscribe_ByAValidLinkForARecordThatIsGone_IsStillDone()
    {
        using var harness = new NewsletterHarness(postgres);

        Assert.True(await harness.Send(new UnsubscribeByTokenCommand(UnsubscribeToken.Create(999_999_999, NewsletterHarness.Key))));
    }

    [Fact]
    [Trait("Spec", "NLS-4")]
    public async Task AfterAnUnsubscribe_OnlyAnExplicitSubscribeBringsTheAddressBack()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        await harness.Send(new SubscribeNewsletterCommand(email));
        var id = Assert.Single(await harness.StoredAsync(email)).Id;
        await harness.Send(new UnsubscribeByTokenCommand(UnsubscribeToken.Create(id, NewsletterHarness.Key)));

        // Reading the preference, or doing anything that is not a subscribe, leaves it unsubscribed.
        var customer = await harness.NewCustomerAsync(email);
        Assert.False((await harness.Send(new GetMyNewsletterQuery(), customer)).Subscribed);
        Assert.Equal(NewsletterStatus.Unsubscribed, Assert.Single(await harness.StoredAsync(email)).Status);

        await harness.Send(new SubscribeNewsletterCommand(email));

        var stored = Assert.Single(await harness.StoredAsync(email));
        Assert.Equal(NewsletterStatus.Subscribed, stored.Status);
        Assert.Equal(3, stored.History.Count);
    }

    [Fact]
    [Trait("Spec", "NLS-2")]
    public async Task TheAccountToggle_TurnsTheCustomersOwnAddressOnAndOff_AndPersists()
    {
        using var harness = new NewsletterHarness(postgres);
        var email = NewsletterHarness.NewEmail();
        var customer = await harness.NewCustomerAsync(email);
        Assert.False((await harness.Send(new GetMyNewsletterQuery(), customer)).Subscribed);

        Assert.True((await harness.Send(new SetMyNewsletterCommand(true), customer)).Subscribed);
        Assert.True((await harness.Send(new GetMyNewsletterQuery(), customer)).Subscribed);

        Assert.False((await harness.Send(new SetMyNewsletterCommand(false), customer)).Subscribed);
        Assert.False((await harness.Send(new GetMyNewsletterQuery(), customer)).Subscribed);

        var stored = Assert.Single(await harness.StoredAsync(email));
        Assert.Equal([(ConsentAction.Subscribed, ConsentSource.Account), (ConsentAction.Unsubscribed, ConsentSource.Account)], stored.History);
    }

    [Fact]
    [Trait("Spec", "NLS-2")]
    public async Task TheAccountToggle_CannotTouchAnotherCustomersAddress()
    {
        using var harness = new NewsletterHarness(postgres);
        var otherEmail = NewsletterHarness.NewEmail();
        await harness.NewCustomerAsync(otherEmail);
        await harness.Send(new SubscribeNewsletterCommand(otherEmail));
        var me = await harness.NewCustomerAsync();

        await harness.Send(new SetMyNewsletterCommand(false), me);

        Assert.Equal(NewsletterStatus.Subscribed, Assert.Single(await harness.StoredAsync(otherEmail)).Status);
    }

    [Fact]
    [Trait("Spec", "NLS-2")]
    public async Task TheAccountToggle_NeedsASignedInCustomer() =>
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            using var harness = new NewsletterHarness(postgres);
            await harness.Send(new GetMyNewsletterQuery());
        });
}
