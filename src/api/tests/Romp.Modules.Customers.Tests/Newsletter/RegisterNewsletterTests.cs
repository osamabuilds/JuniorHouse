using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Newsletter;

/// <summary>customers/newsletter NL8 (NLS-4, NLS-5, FR-41): the registration checkbox subscribes only when ticked and never re-subscribes an unsubscribed address.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class RegisterNewsletterTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"reg.nl.{Guid.NewGuid():N}@example.test";

    private static Task<List<(NewsletterStatus Status, ConsentSource Source)>> StoredAsync(AccountsHarness harness, string email) =>
        harness.Read(async db =>
        {
            var rows = await db.Newsletter.AsNoTracking().Include(s => s.History).Where(s => s.Email == email).ToListAsync();
            return rows.SelectMany(s => s.History.Select(h => (s.Status, h.Source))).ToList();
        });

    [Fact]
    [Trait("Spec", "NLS-5")]
    public async Task Register_WithTheBoxTicked_SubscribesTheAddress_WithTheRegistrationSource()
    {
        using var harness = new AccountsHarness(postgres);
        var email = NewEmail();

        var result = await harness.Send(RegisterTests.Register(email) with { SubscribeNewsletter = true });

        Assert.Equal(RegisterStatus.Registered, result.Status);
        Assert.Equal([(NewsletterStatus.Subscribed, ConsentSource.Registration)], await StoredAsync(harness, email));
    }

    [Fact]
    [Trait("Spec", "NLS-4")]
    public async Task Register_WithTheBoxUnticked_SubscribesNobody()
    {
        using var harness = new AccountsHarness(postgres);
        var email = NewEmail();

        await harness.Send(RegisterTests.Register(email));

        Assert.Empty(await StoredAsync(harness, email));
    }

    [Fact]
    [Trait("Spec", "NLS-4")]
    public async Task Register_WithTheBoxUnticked_LeavesAnEarlierUnsubscribeAlone()
    {
        using var harness = new AccountsHarness(postgres);
        var email = NewEmail();
        await harness.Seed(db =>
        {
            var subscription = NewsletterSubscription.Start(email, ConsentSource.Footer, DateTimeOffset.UtcNow);
            subscription.Unsubscribe(ConsentSource.Link, DateTimeOffset.UtcNow);
            db.Newsletter.Add(subscription);
        });

        await harness.Send(RegisterTests.Register(email));

        var stored = await StoredAsync(harness, email);
        Assert.All(stored, row => Assert.Equal(NewsletterStatus.Unsubscribed, row.Status));
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    [Trait("Spec", "NLS-4")]
    public async Task Register_WithTheBoxTicked_AfterAnUnsubscribe_IsAnExplicitResubscribe()
    {
        using var harness = new AccountsHarness(postgres);
        var email = NewEmail();
        await harness.Seed(db =>
        {
            var subscription = NewsletterSubscription.Start(email, ConsentSource.Footer, DateTimeOffset.UtcNow);
            subscription.Unsubscribe(ConsentSource.Link, DateTimeOffset.UtcNow);
            db.Newsletter.Add(subscription);
        });

        await harness.Send(RegisterTests.Register(email) with { SubscribeNewsletter = true });

        var stored = await StoredAsync(harness, email);
        Assert.Equal(3, stored.Count);
        Assert.All(stored, row => Assert.Equal(NewsletterStatus.Subscribed, row.Status));
    }

    [Fact]
    [Trait("Spec", "NLS-5")]
    public async Task Register_WithAnEmailAlreadyTaken_SubscribesNobody()
    {
        using var harness = new AccountsHarness(postgres);
        var email = NewEmail();
        await harness.Send(RegisterTests.Register(email));

        var again = await harness.Send(RegisterTests.Register(email) with { SubscribeNewsletter = true });

        Assert.Equal(RegisterStatus.EmailTaken, again.Status);
        Assert.Empty(await StoredAsync(harness, email));
    }
}
