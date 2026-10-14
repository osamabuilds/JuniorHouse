using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Infrastructure.Products;
using Romp.Modules.Catalog.Tests.Support;
using Romp.Modules.Labelling.Contracts;

namespace Romp.Modules.Catalog.Tests.Products;

/// <summary>PRD-13..15 and ADR 0014: Catalog's copy of Labelling's variants follows its events, however they arrive.</summary>
[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class LabellingVariantConsumerTests(CatalogPostgres postgres)
{
    private static VariantGtinAssignedEvent Assigned(LabelledVariantInfo v) =>
        new(v.VariantId, v.StyleId, v.ColourId, v.SizeId, v.SkuCode, v.Gtin);

    private static GtinVoidedEvent Voided(LabelledVariantInfo v) =>
        new(v.VariantId, v.StyleId, v.ColourId, v.SizeId, v.SkuCode, v.Gtin, "Error");

    private static async Task DeliverAsync<TEvent>(CatalogHost host, long messageId, TEvent domainEvent)
        where TEvent : class
    {
        await using var scope = host.Scope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
        context.MessageId = messageId;
        context.Schema = "CTLG";
        switch (domainEvent)
        {
            case VariantGtinAssignedEvent assigned:
                await scope.ServiceProvider.GetRequiredService<VariantGtinAssignedConsumer>().HandleAsync(assigned, default);
                break;
            case GtinVoidedEvent voided:
                await scope.ServiceProvider.GetRequiredService<GtinVoidedConsumer>().HandleAsync(voided, default);
                break;
        }
    }

    private static long _messageSeed = 800_000;

    private static long NextMessage() => Interlocked.Increment(ref _messageSeed);

    private static async Task<(string? Sku, bool Sellable)> StateOfAsync(CatalogHost host, long productId, short colour, short size)
    {
        var product = await host.SendAsync(new GetProductQuery(productId));
        var variant = product.Variants.Single(v => v.ColourId == colour && v.SizeId == size);
        return (variant.SkuCode, variant.IsSellable);
    }

    [Fact]
    [Trait("Spec", "PRD-13")]
    public async Task AVariantBecomesSelectable_AsSoonAsItsGtinEventArrives_WithNoManualStep()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        Assert.Equal((null, false), await StateOfAsync(host, product.Id, 1, 4));
        var label = host.Label(product.StyleId, 1, 4);

        await DeliverAsync(host, NextMessage(), Assigned(label));

        Assert.Equal((label.SkuCode, true), await StateOfAsync(host, product.Id, 1, 4));
    }

    [Fact]
    [Trait("Spec", "PRD-14")]
    public async Task AVoidedGtin_StopsTheVariantBeingSellable()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var label = host.Label(product.StyleId, 1, 4);
        await DeliverAsync(host, NextMessage(), Assigned(label));

        host.Labelling.Variants.Remove(label);
        var voidedLabel = label with { GtinVoided = true };
        host.Labelling.Variants.Add(voidedLabel);
        await DeliverAsync(host, NextMessage(), Voided(voidedLabel));

        var view = (await host.SendAsync(new GetProductQuery(product.Id))).Variants.Single(v => v.ColourId == 1 && v.SizeId == 4);
        Assert.False(view.IsSellable);
        Assert.Equal("GtinVoided", view.Availability);
    }

    [Fact]
    [Trait("Spec", "PRD-15")]
    public async Task TheSameMessageDeliveredTwice_ChangesNothingTheSecondTime()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var label = host.Label(product.StyleId, 1, 4);
        var message = NextMessage();

        await DeliverAsync(host, message, Assigned(label));
        await DeliverAsync(host, message, Assigned(label));

        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.LabelledVariants.CountAsync(l => l.LabellingVariantId == label.VariantId));
        Assert.Equal(1, await db.Set<Romp.BuildingBlocks.Persistence.Inbox.InboxMessage>().CountAsync(m => m.MessageId == message));
    }

    [Fact]
    [Trait("Spec", "PRD-15")]
    public async Task EventsInTheWrongOrder_GiveTheSameResultAsInOrder()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var label = host.Label(product.StyleId, 1, 4);
        host.Labelling.Variants.Remove(label);
        var voided = label with { GtinVoided = true };
        host.Labelling.Variants.Add(voided);

        // The void (the newer fact, which Labelling now reports) arrives first, then the older assignment.
        await DeliverAsync(host, NextMessage(), Voided(voided));
        await DeliverAsync(host, NextMessage(), Assigned(label));

        var view = (await host.SendAsync(new GetProductQuery(product.Id))).Variants.Single(v => v.ColourId == 1 && v.SizeId == 4);
        Assert.Equal("GtinVoided", view.Availability);
    }

    [Fact]
    [Trait("Spec", "PRD-14")]
    public async Task AVoidedGtinThatLabellingReplaced_IsSellableAgain()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var first = host.Label(product.StyleId, 1, 4);
        await DeliverAsync(host, NextMessage(), Assigned(first));
        host.Labelling.Variants.Remove(first);
        var voided = first with { GtinVoided = true };
        host.Labelling.Variants.Add(voided);
        await DeliverAsync(host, NextMessage(), Voided(voided));

        // Labelling re-issues: the same variant now holds a new GTIN.
        host.Labelling.Variants.Remove(voided);
        var reissued = voided with { GtinVoided = false, Gtin = "9999999999999" };
        host.Labelling.Variants.Add(reissued);
        await DeliverAsync(host, NextMessage(), Assigned(reissued));

        var view = (await host.SendAsync(new GetProductQuery(product.Id))).Variants.Single(v => v.ColourId == 1 && v.SizeId == 4);
        Assert.True(view.IsSellable);
        Assert.Equal("9999999999999", view.Gtin);
    }

    [Fact]
    public async Task WhenLabellingCannotAnswer_TheEventsOwnFactsAreUsed()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var label = host.Label(product.StyleId, 1, 4);
        host.Labelling.Variants.Clear();

        await DeliverAsync(host, NextMessage(), Assigned(label));

        Assert.Equal((label.SkuCode, true), await StateOfAsync(host, product.Id, 1, 4));
    }

    [Fact]
    [Trait("Spec", "PRD-13")]
    public async Task AVariantOfAPublishedProductBecomingSellable_TellsTheStorefront()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));
        var label = host.Label(ready.StyleId, 2, 5);

        await DeliverAsync(host, NextMessage(), Assigned(label));

        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Set<OutboxMessage>().CountAsync(m => m.EventType == "ProductChangedEvent" && m.AggregateId == ready.Id));
    }

    [Fact]
    public async Task AnEventForAStyleWithNoProductYet_IsStoredForWhenTheProductIsCreated()
    {
        using var host = new CatalogHost(postgres);
        var styleId = await host.NewStyleAsync();
        var label = host.Label(styleId, 1, 4);
        await DeliverAsync(host, NextMessage(), Assigned(label));

        var product = await host.NewProductAsync(styleId);

        Assert.True(product.Variants.Single(v => v.ColourId == 1 && v.SizeId == 4).IsSellable);
    }

    [Fact]
    [Trait("Spec", "PRD-15")]
    public async Task RefreshFromLabelling_GivesTheSameRowsAsTheEvents_AndRunningItTwiceChangesNothing()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        host.Label(product.StyleId, 1, 4);
        host.Label(product.StyleId, 2, 6, voided: true);

        var once = await host.SendAsync(new RefreshVariantsFromLabellingCommand(product.Id));
        var twice = await host.SendAsync(new RefreshVariantsFromLabellingCommand(product.Id));

        Assert.Equal(once.Variants, twice.Variants);
        Assert.Equal(1, once.SellableVariants);
        Assert.Equal("GtinVoided", once.Variants.Single(v => v.ColourId == 2 && v.SizeId == 6).Availability);
        await using var db = postgres.NewContext();
        Assert.Equal(2, await db.LabelledVariants.CountAsync(l => l.StyleId == product.StyleId));
    }
}
