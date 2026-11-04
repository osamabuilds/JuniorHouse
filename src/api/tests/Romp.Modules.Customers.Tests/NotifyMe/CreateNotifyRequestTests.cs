using Romp.BuildingBlocks.Application;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N4 (NTM-2, NTM-3, NTM-6): capturing a request for a sold-out variant, on a real PostgreSQL.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class CreateNotifyRequestTests(CustomersPostgres postgres)
{
    private static string NewEmail() => $"ntm.{Guid.NewGuid():N}@example.test";

    [Fact]
    [Trait("Spec", "NTM-2")]
    public async Task ASoldOutVariant_RecordsOneOpenRequest_WithTheNormalisedAddress()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, available: false);
        var email = NewEmail();

        await harness.Send(new CreateNotifyRequestCommand(variant, "  " + email.ToUpperInvariant() + " "));

        var row = Assert.Single(await harness.NotifyRequestsAsync(variant));
        Assert.Equal((email, NotifyStatus.Open), (row.Email, row.Status));
    }

    [Fact]
    [Trait("Spec", "NTM-3")]
    public async Task TheSameAddressTwice_LeavesOneRequest_AndBothCallsSucceed()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, available: false);
        var email = NewEmail();

        await harness.Send(new CreateNotifyRequestCommand(variant, email));
        await harness.Send(new CreateNotifyRequestCommand(variant, email.ToUpperInvariant()));

        Assert.Single(await harness.NotifyRequestsAsync(variant));
    }

    [Fact]
    [Trait("Spec", "NTM-3")]
    public async Task ConcurrentRepeats_LeaveOneRequest()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, available: false);
        var email = NewEmail();

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => harness.Send(new CreateNotifyRequestCommand(variant, email))));

        Assert.Single(await harness.NotifyRequestsAsync(variant));
    }

    [Fact]
    [Trait("Spec", "NTM-3")]
    public async Task TwoAddresses_AndTwoVariants_AreSeparateRequests()
    {
        using var harness = new NewsletterHarness(postgres);
        var first = NewsletterHarness.NewVariantId();
        var second = first + 1;
        harness.Catalog.PublishVariant(first);
        harness.Catalog.PublishVariant(second);
        var email = NewEmail();

        await harness.Send(new CreateNotifyRequestCommand(first, email));
        await harness.Send(new CreateNotifyRequestCommand(first, NewEmail()));
        await harness.Send(new CreateNotifyRequestCommand(second, email));

        Assert.Equal(2, (await harness.NotifyRequestsAsync(first)).Count);
        Assert.Single(await harness.NotifyRequestsAsync(second));
    }

    [Theory]
    [Trait("Spec", "NTM-6")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task AnInvalidAddress_IsRefused_AndNothingIsRecorded(string? email)
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => harness.Send(new CreateNotifyRequestCommand(variant, email)));

        Assert.True(ex.Errors.ContainsKey(nameof(CreateNotifyRequestCommand.Email)));
        Assert.Empty(await harness.NotifyRequestsAsync(variant));
    }

    [Fact]
    [Trait("Spec", "NTM-6")]
    public async Task AVariantThatIsInStock_IsRefused_AndNothingIsRecorded()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant, available: true);

        await Assert.ThrowsAsync<ValidationException>(() => harness.Send(new CreateNotifyRequestCommand(variant, NewEmail())));

        Assert.Empty(await harness.NotifyRequestsAsync(variant));
    }

    [Fact]
    [Trait("Spec", "NTM-6")]
    public async Task AnUnknownVariant_IsRefused_AndNothingIsRecorded()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();

        await Assert.ThrowsAsync<ValidationException>(() => harness.Send(new CreateNotifyRequestCommand(variant, NewEmail())));
        await Assert.ThrowsAsync<ValidationException>(() => harness.Send(new CreateNotifyRequestCommand(0, NewEmail())));

        Assert.Empty(await harness.NotifyRequestsAsync(variant));
    }

    [Fact]
    [Trait("Spec", "NTM-6")]
    public async Task TheCapPerVariant_StopsMoreRequests_ButARepeatStillSucceeds()
    {
        using var harness = new NewsletterHarness(postgres, new CustomersOptions { MaxNotifyPerVariant = 2, LinkSigningKey = NewsletterHarness.Key });
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        var first = NewEmail();
        await harness.Send(new CreateNotifyRequestCommand(variant, first));
        await harness.Send(new CreateNotifyRequestCommand(variant, NewEmail()));

        await Assert.ThrowsAsync<ValidationException>(() => harness.Send(new CreateNotifyRequestCommand(variant, NewEmail())));
        await harness.Send(new CreateNotifyRequestCommand(variant, first));

        Assert.Equal(2, (await harness.NotifyRequestsAsync(variant)).Count);
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public async Task AClosedRequest_DoesNotBlockANewOne()
    {
        using var harness = new NewsletterHarness(postgres);
        var variant = NewsletterHarness.NewVariantId();
        harness.Catalog.PublishVariant(variant);
        var email = NewEmail();
        await harness.Send(new CreateNotifyRequestCommand(variant, email));
        await harness.Use(async sp =>
        {
            var db = (Romp.Modules.Customers.Infrastructure.Persistence.CustDbContext)sp.GetService(typeof(Romp.Modules.Customers.Infrastructure.Persistence.CustDbContext))!;
            var row = db.NotifyRequests.Single(r => r.VariantId == variant);
            row.MarkNotified(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            return 0;
        });

        await harness.Send(new CreateNotifyRequestCommand(variant, email));

        Assert.Equal([NotifyStatus.Notified, NotifyStatus.Open], (await harness.NotifyRequestsAsync(variant)).Select(r => r.Status));
    }
}
