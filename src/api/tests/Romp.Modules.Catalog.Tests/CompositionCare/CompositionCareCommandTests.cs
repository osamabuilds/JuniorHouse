using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.CompositionCare;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.CompositionCare;

[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class CompositionCareCommandTests(CatalogPostgres postgres)
{
    private static CompositionRowInput[] Cotton95() => [new(1, 95m), new(3, 5m)];

    [Fact]
    [Trait("Spec", "CMP-1")]
    public async Task Composition_CanBeSavedIncomplete_ButIsFlaggedUntilItTotalsExactly100()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var partial = await host.SendAsync(new SetCompositionCommand(product.Id, [new(1, 95m)], product.Version, "Test Person"));
        var complete = await host.SendAsync(new SetCompositionCommand(product.Id, Cotton95(), partial.Version, "Test Person"));

        Assert.False(partial.IsComplete);
        Assert.Equal(95m, partial.Total);
        Assert.True(complete.IsComplete);
        Assert.Equal(100m, complete.Total);
        Assert.Equal(["Cotton", "Elastane"], complete.Rows.Select(r => r.FibreName).ToArray());
    }

    [Fact]
    [Trait("Spec", "CMP-7")]
    public async Task ThirdsWithOneDecimal_TotalExactlyOneHundred()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var thirds = await host.SendAsync(new SetCompositionCommand(product.Id, [new(1, 33.3m), new(2, 33.3m), new(3, 33.4m)], product.Version, "Test Person"));

        Assert.True(thirds.IsComplete);
    }

    [Fact]
    [Trait("Spec", "CMP-6")]
    public async Task BadRows_AreRefusedWithAMessage()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCompositionCommand(product.Id, [new(1, 50m), new(1, 50m)], product.Version, "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCompositionCommand(product.Id, [new(1, 100.01m)], product.Version, "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCompositionCommand(product.Id, [new(1, -5m)], product.Version, "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCompositionCommand(product.Id, [new(99, 100m)], product.Version, "Test Person")));
    }

    [Fact]
    public async Task SavingComposition_ReplacesAllRows_InOneTransaction()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var first = await host.SendAsync(new SetCompositionCommand(product.Id, Cotton95(), product.Version, "Test Person"));

        var second = await host.SendAsync(new SetCompositionCommand(product.Id, [new(1, 100m)], first.Version, "Test Person"));

        Assert.Single(second.Rows);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.CompositionLines.CountAsync(l => l.ProductId == product.Id));
    }

    [Fact]
    [Trait("Spec", "CMP-5")]
    public async Task ACompositionEditBySecondEditor_WithAStaleVersion_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await host.SendAsync(new SetCompositionCommand(product.Id, Cotton95(), product.Version, "Editor One"));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            host.SendAsync(new SetCompositionCommand(product.Id, [new(1, 100m)], product.Version, "Editor Two")));
    }

    [Fact]
    [Trait("Spec", "CMP-4")]
    public async Task EveryCareInstruction_HasATextNameAndASymbolKey()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var care = await host.SendAsync(new SetCareCommand(product.Id, [1, 2], "Wash inside out", product.Version, "Test Person"));

        Assert.Equal(2, care.Care.Count);
        Assert.All(care.Care, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.SymbolCode));
        });
        Assert.Equal("Wash inside out", care.CareNote);
    }

    [Fact]
    [Trait("Spec", "CMP-2")]
    public async Task UnknownCareInstructions_AndHtmlInTheNote_AreRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCareCommand(product.Id, [99], null, product.Version, "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCareCommand(product.Id, [1], "<b>bold</b>", product.Version, "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCareCommand(product.Id, [1], new string('a', 501), product.Version, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "CMP-3")]
    public async Task GetCompositionAndCare_ReturnsRowsTotalCareAndNote()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var composition = await host.SendAsync(new SetCompositionCommand(product.Id, Cotton95(), product.Version, "Test Person"));
        await host.SendAsync(new SetCareCommand(product.Id, [2], "Cool iron", composition.Version, "Test Person"));

        var read = await host.SendAsync(new GetCompositionAndCareQuery(product.Id));

        Assert.True(read.IsComplete);
        Assert.Equal("no-bleach", Assert.Single(read.Care).SymbolCode);
        Assert.Equal("Cool iron", read.CareNote);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetCompositionAndCareQuery(long.MaxValue)));
    }

    [Fact]
    [Trait("Spec", "CMP-8")]
    public async Task ANewFibreInReferenceData_IsUsableWithNoCodeChange()
    {
        using var host = new CatalogHost(postgres);
        host.Lookups.Fibres[50] = new(50, "BAMBOO", "Bamboo");
        var product = await host.NewProductAsync();

        var saved = await host.SendAsync(new SetCompositionCommand(product.Id, [new(50, 100m)], product.Version, "Test Person"));

        Assert.Equal("Bamboo", Assert.Single(saved.Rows).FibreName);
    }

    [Fact]
    [Trait("Spec", "CMP-5")]
    public async Task ChangingCompositionOrCareOfAPublishedProduct_WritesTheChangedEvent()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var published = (await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"))).Product;
        var care = await host.SendAsync(new GetCompositionAndCareQuery(ready.Id));

        await host.SendAsync(new SetCareCommand(ready.Id, [1], "New note", care.Version, "Test Person"));

        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Set<Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage>().CountAsync(m => m.EventType == "ProductChangedEvent" && m.AggregateId == ready.Id));
    }

    [Fact]
    [Trait("Spec", "CMP-1")]
    public async Task AFinalCompositionOf95Percent_BlocksThePublishWithTheActualTotal()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var current = await host.SendAsync(new GetProductQuery(ready.Id));
        await host.SendAsync(new SetCompositionCommand(ready.Id, [new(1, 95m)], current.Version, "Test Person"));

        var result = await host.SendAsync(new PublishProductCommand(ready.Id, (await host.SendAsync(new GetProductQuery(ready.Id))).Version, "Test Person"));

        var failure = Assert.Single(result.Failures);
        Assert.Equal("COMPOSITION_TOTAL", failure.Code);
        Assert.Contains("95%", failure.Message, StringComparison.Ordinal);
    }
}
