using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.Newsletter;

namespace Romp.Modules.Customers.Tests.Newsletter;

/// <summary>customers/newsletter NL1 (NLS-1, NLS-3, NLS-4, NLS-5): the consent rules, email checks and the signed unsubscribe value, all pure.</summary>
public sealed class NewsletterSubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
    private const string Key = "a-test-signing-key-long-enough-for-hmac";

    [Theory]
    [Trait("Spec", "NLS-1")]
    [InlineData("  Sara@Example.COM ", "sara@example.com")]
    [InlineData("a.b+tag@mail.example.pk", "a.b+tag@mail.example.pk")]
    public void Normalize_TrimsAndLowersTheAddress(string raw, string expected) =>
        Assert.Equal(expected, NewsletterEmail.Normalize(raw));

    [Theory]
    [Trait("Spec", "NLS-1")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("@example.com")]
    [InlineData("sara@nodot")]
    [InlineData("sara@example.")]
    [InlineData("sara@.example.com")]
    [InlineData("sara@exa..mple.com")]
    [InlineData("sa ra@example.com")]
    [InlineData("sara@example.com\r\nBcc: victim@example.com")]
    [InlineData("<sara@example.com>")]
    public void Normalize_RefusesAnythingThatIsNotAnAddress(string? raw) =>
        Assert.Null(NewsletterEmail.Normalize(raw));

    [Fact]
    [Trait("Spec", "NLS-1")]
    public void Normalize_RefusesAnAddressOver254Characters() =>
        Assert.Null(NewsletterEmail.Normalize(new string('a', 250) + "@example.com"));

    [Fact]
    [Trait("Spec", "NLS-5")]
    public void Start_RecordsASubscribedAddress_WithOneHistoryRowForItsSource()
    {
        var subscription = NewsletterSubscription.Start("sara@example.com", ConsentSource.Footer, Now);

        Assert.True(subscription.IsSubscribed);
        var row = Assert.Single(subscription.History);
        Assert.Equal((ConsentAction.Subscribed, ConsentSource.Footer, Now), (row.Action, row.Source, row.OccurredAt));
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public void Start_RefusesAnAddressThatWasNotNormalised() =>
        Assert.Throws<ArgumentException>(() => NewsletterSubscription.Start("Sara@Example.com", ConsentSource.Footer, Now));

    [Fact]
    [Trait("Spec", "NLS-1")]
    public void Subscribe_WhenAlreadySubscribed_ChangesNothingAndAddsNoHistory()
    {
        var subscription = NewsletterSubscription.Start("sara@example.com", ConsentSource.Footer, Now);

        var changed = subscription.Subscribe(ConsentSource.Account, Now.AddDays(1));

        Assert.False(changed);
        Assert.Single(subscription.History);
        Assert.Equal(Now, subscription.ChangedAt);
    }

    [Fact]
    [Trait("Spec", "NLS-4")]
    public void Unsubscribe_HoldsUntilTheAddressSubscribesAgainOnPurpose()
    {
        var subscription = NewsletterSubscription.Start("sara@example.com", ConsentSource.Footer, Now);

        Assert.True(subscription.Unsubscribe(ConsentSource.Link, Now.AddDays(1)));
        Assert.False(subscription.IsSubscribed);
        Assert.False(subscription.Unsubscribe(ConsentSource.Link, Now.AddDays(2)));
        Assert.Equal(2, subscription.History.Count);

        Assert.True(subscription.Subscribe(ConsentSource.Registration, Now.AddDays(3)));
        Assert.True(subscription.IsSubscribed);
        Assert.Equal(
            [ConsentAction.Subscribed, ConsentAction.Unsubscribed, ConsentAction.Subscribed],
            subscription.History.Select(h => h.Action));
    }

    [Fact]
    [Trait("Spec", "NLS-1")]
    public void Subscribe_AfterAnUnsubscribe_AllowsANewWelcome()
    {
        var subscription = NewsletterSubscription.Start("sara@example.com", ConsentSource.Footer, Now);
        subscription.MarkWelcomeSent(Now);
        subscription.Unsubscribe(ConsentSource.Link, Now.AddDays(1));

        subscription.Subscribe(ConsentSource.Footer, Now.AddDays(2));

        Assert.Null(subscription.WelcomeSentAt);
    }

    [Fact]
    [Trait("Spec", "NLS-5")]
    public void QueueWelcome_BeforeTheRecordIsSaved_IsRefused() =>
        Assert.Throws<InvalidOperationException>(() =>
            NewsletterSubscription.Start("sara@example.com", ConsentSource.Footer, Now).QueueWelcome());

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Event_CarriesTheKindAndTheRecordId_AndNoAddress()
    {
        var message = new CustomerEmailRequestedEvent(CustomerEmailKind.NewsletterWelcome, 42);
        var json = System.Text.Json.JsonSerializer.Serialize(message);

        Assert.DoesNotContain("@", json, StringComparison.Ordinal);
        var back = System.Text.Json.JsonSerializer.Deserialize<CustomerEmailRequestedEvent>(json)!;
        Assert.Equal((CustomerEmailKind.NewsletterWelcome, 42L), (back.Kind, back.RelatedId));
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Token_RoundTripsTheRecordId_AndCarriesNoEmail()
    {
        var token = UnsubscribeToken.Create(1234, Key);

        Assert.Equal(1234L, UnsubscribeToken.Verify(token, Key));
        Assert.DoesNotContain("@", token, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Token_SignedWithAnotherKey_IsRefused() =>
        Assert.Null(UnsubscribeToken.Verify(UnsubscribeToken.Create(5, "another-signing-key-of-sufficient-length"), Key));

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Token_WithTheIdSwappedForAnotherRecord_IsRefused()
    {
        var mine = UnsubscribeToken.Create(5, Key);
        var theirs = UnsubscribeToken.Create(6, Key);
        var forged = theirs.Split('.')[0] + "." + mine.Split('.')[1];

        Assert.Null(UnsubscribeToken.Verify(forged, Key));
    }

    [Theory]
    [Trait("Spec", "NLS-3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abc.def.ghi")]
    [InlineData(".")]
    [InlineData("NQ.")]
    [InlineData(".sig")]
    [InlineData("!!!.???")]
    public void Token_ThatIsMalformed_IsRefusedWithoutThrowing(string? token) =>
        Assert.Null(UnsubscribeToken.Verify(token, Key));

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Token_ThatIsTruncatedOrOversized_IsRefused()
    {
        var token = UnsubscribeToken.Create(77, Key);

        Assert.Null(UnsubscribeToken.Verify(token[..^3], Key));
        Assert.Null(UnsubscribeToken.Verify(token + new string('A', 300), Key));
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Token_WithAShortKey_CannotBeCreated() =>
        Assert.Throws<InvalidOperationException>(() => UnsubscribeToken.Create(1, "short"));

    [Fact]
    [Trait("Spec", "NLS-3")]
    public void Options_RejectAShortSigningKey_AndOnlyARealKeyCountsAsProduction()
    {
        Assert.False(CustomersOptions.IsValid(new CustomersOptions { LinkSigningKey = "short" }));
        Assert.True(CustomersOptions.IsValid(new CustomersOptions()));
        Assert.False(CustomersOptions.HasProductionKey(new CustomersOptions()));
        Assert.True(CustomersOptions.HasProductionKey(new CustomersOptions { LinkSigningKey = new string('k', 40) }));
    }
}
