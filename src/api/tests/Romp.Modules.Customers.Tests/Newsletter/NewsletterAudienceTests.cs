using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Customers.Application.Newsletter;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Newsletter;

/// <summary>customers/newsletter NL6 (NLS-4, NLS-7): who a future campaign sender may mail, read through the contract only.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class NewsletterAudienceTests(CustomersPostgres postgres)
{
    private static Task<IReadOnlyList<SubscribedAddress>> ListAsync(NewsletterHarness harness, long afterId, int take) =>
        harness.Use(sp => sp.GetRequiredService<INewsletterAudience>().ListSubscribedAsync(afterId, take, CancellationToken.None));

    [Fact]
    [Trait("Spec", "NLS-4")]
    public async Task TheAudience_HoldsSubscribedAddressesOnly()
    {
        using var harness = new NewsletterHarness(postgres);
        var stays = NewsletterHarness.NewEmail();
        var leaves = NewsletterHarness.NewEmail();
        await harness.Send(new SubscribeNewsletterCommand(stays));
        await harness.Send(new SubscribeNewsletterCommand(leaves));
        var leavesId = Assert.Single(await harness.StoredAsync(leaves)).Id;
        await harness.Send(new UnsubscribeByTokenCommand(UnsubscribeToken.Create(leavesId, NewsletterHarness.Key)));

        var all = await ListAsync(harness, 0, 500);

        Assert.Contains(all, a => a.Email == stays);
        Assert.DoesNotContain(all, a => a.Email == leaves);
    }

    [Fact]
    [Trait("Spec", "NLS-7")]
    public async Task TheAudience_PagesByKeyset_InIdOrder()
    {
        using var harness = new NewsletterHarness(postgres);
        foreach (var _ in Enumerable.Range(0, 5))
        {
            await harness.Send(new SubscribeNewsletterCommand(NewsletterHarness.NewEmail()));
        }

        var first = await ListAsync(harness, 0, 2);
        var second = await ListAsync(harness, first[^1].Id, 2);

        Assert.Equal(2, first.Count);
        Assert.True(second.Count >= 1);
        Assert.True(second[0].Id > first[^1].Id);
        Assert.Equal(first.Select(a => a.Id).OrderBy(i => i), first.Select(a => a.Id));
    }

    [Theory]
    [Trait("Spec", "NLS-7")]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100_000, INewsletterAudience.MaxTake)]
    public async Task TheAudience_ClampsThePageSize(int asked, int expectedAtMost)
    {
        using var harness = new NewsletterHarness(postgres);
        await harness.Send(new SubscribeNewsletterCommand(NewsletterHarness.NewEmail()));
        await harness.Send(new SubscribeNewsletterCommand(NewsletterHarness.NewEmail()));

        var page = await ListAsync(harness, 0, asked);

        Assert.InRange(page.Count, 1, expectedAtMost);
    }
}
