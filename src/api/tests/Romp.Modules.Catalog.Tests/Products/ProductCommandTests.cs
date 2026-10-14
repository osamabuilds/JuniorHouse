using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Products;

[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class ProductCommandTests(CatalogPostgres postgres)
{
    private static string Outbox(CatalogPostgres postgres, string eventType, long productId)
    {
        using var db = postgres.NewContext();
        return string.Join('|', db.Set<OutboxMessage>().Where(m => m.EventType == eventType && m.AggregateId == productId).OrderBy(m => m.Id).Select(m => m.Payload));
    }

    private static int OutboxCount(CatalogPostgres postgres, string eventType, long productId)
    {
        using var db = postgres.NewContext();
        return db.Set<OutboxMessage>().Count(m => m.EventType == eventType && m.AggregateId == productId);
    }

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task CreatingAProduct_ListsSixPlannedVariants_AndOnlyLabelledOnesShowSkuAndGtin()
    {
        using var host = new CatalogHost(postgres);
        var styleId = await host.NewStyleAsync(colours: [1, 2], sizes: [4, 5, 6]);
        var label = host.Label(styleId, 1, 4);

        var product = await host.NewProductAsync(styleId);

        Assert.Equal(6, product.Variants.Count);
        var labelled = product.Variants.Single(v => v.ColourId == 1 && v.SizeId == 4);
        Assert.Equal(label.SkuCode, labelled.SkuCode);
        Assert.Equal(label.Gtin, labelled.Gtin);
        Assert.Equal("Sellable", labelled.Availability);
        Assert.All(product.Variants.Where(v => v != labelled), v =>
        {
            Assert.Null(v.SkuCode);
            Assert.Equal("NotYetAvailable", v.Availability);
        });
        Assert.Equal(1, product.SellableVariants);
        Assert.Equal("Red", labelled.ColourName);
        Assert.Equal("1-2 Years", labelled.SizeName);
    }

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task APlannedVariant_StartsAtTheStylesTargetRetailPrice()
    {
        using var host = new CatalogHost(postgres);

        var product = await host.NewProductAsync(await host.NewStyleAsync(retailPrice: 2499m));

        Assert.All(product.Variants, v => Assert.Equal(2499m, v.Price));
        Assert.Equal("Draft", product.Status);
        Assert.Matches("^/girls/dresses/floral-dress-[a-z0-9]{8}$", product.Path);
    }

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task ASecondProductForAStyle_IsRefused_NamingTheExistingOne()
    {
        using var host = new CatalogHost(postgres);
        var first = await host.NewProductAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => host.NewProductAsync(first.StyleId));

        Assert.Contains($"product {first.Id}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARetiredStyle_CannotGetAProduct_AndAnUnknownOneIsNotFound()
    {
        using var host = new CatalogHost(postgres);

        await Assert.ThrowsAsync<DomainException>(async () => await host.NewProductAsync(await host.NewStyleAsync(active: false)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.NewProductAsync(long.MaxValue));
    }

    [Fact]
    public async Task UnknownGenderCategoryOrAgeBracket_AreRefusedWithFieldErrors()
    {
        using var host = new CatalogHost(postgres);
        var style = await host.NewStyleAsync();

        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new CreateProductCommand(style, "Dress", "d", 99, 98, 97, "Test Person")));

        Assert.Equal(["GenderId", "CategoryId", "AgeBracketId"], error.Errors.Keys.ToArray());
    }

    [Fact]
    [Trait("Spec", "URL-4")]
    public async Task TenProductsWithTheSameNameCreatedAtOnce_GetDistinctStableSlugs()
    {
        using var host = new CatalogHost(postgres);
        var name = "Race Dress " + Guid.NewGuid().ToString("N")[..8];
        var styles = new List<long>();
        for (var i = 0; i < 10; i++)
        {
            styles.Add(await host.NewStyleAsync());
        }

        var products = await Task.WhenAll(styles.Select(s => host.NewProductAsync(s, name)));

        var slugs = products.Select(p => p.Slug).ToList();
        Assert.Equal(10, slugs.Distinct().Count());
        var baseSlug = SlugPolicyForTest(name);
        var expected = new[] { baseSlug }.Concat(Enumerable.Range(2, 9).Select(n => $"{baseSlug}-{n}")).Order().ToArray();
        Assert.Equal(expected, slugs.Order().ToArray());
    }

    private static string SlugPolicyForTest(string name) => Romp.Modules.Catalog.Domain.Urls.SlugPolicy.Normalise(name);

    [Fact]
    [Trait("Spec", "PRD-5")]
    public async Task TheSecondEditorWithAStaleVersion_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        var first = new UpdateProductContentCommand(product.Id, "First edit", "d", 2, 3, 4, null, product.Version, "Editor One");
        var second = new UpdateProductContentCommand(product.Id, "Second edit", "d", 2, 3, 4, null, product.Version, "Editor Two");

        var saved = await host.SendAsync(first);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => host.SendAsync(second));

        Assert.NotEqual(product.Version, saved.Version);
        Assert.Equal("First edit", (await host.SendAsync(new GetProductQuery(product.Id))).Name);
    }

    [Fact]
    [Trait("Spec", "PRD-5")]
    public async Task AChangeToTheChildren_MovesTheVersion_SoAnotherEditorSeesAConflict()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var priced = await host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1500m, null)], product.Version, "Editor One"));

        Assert.NotEqual(product.Version, priced.Version);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            host.SendAsync(new SetCollectionsCommand(product.Id, [1], product.Version, "Editor Two")));
    }

    [Fact]
    [Trait("Spec", "PRD-2")]
    public async Task ACompareAtPriceNotAboveThePrice_IsRefusedWithAMessage()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1000m, 1000m)], product.Version, "Test Person")));

        Assert.Contains("compare-at price must be higher", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [Trait("Spec", "PRD-8")]
    public async Task ANonPositivePrice_IsRefused(double price)
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, (decimal)price, null)], product.Version, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "PRD-7")]
    public async Task APriceChange_RecordsTheOldAndNewPrice_WhoAndWhen()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync(await host.NewStyleAsync(retailPrice: 1000m));

        await host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1200m, 1500m)], product.Version, "Ayesha Khan"));

        await using var db = postgres.NewContext();
        var variantId = await db.Variants.Where(v => v.ProductId == product.Id && v.ColourId == 1 && v.SizeId == 4).Select(v => v.Id).SingleAsync();
        var history = await db.PriceHistory.SingleAsync(h => h.VariantId == variantId);
        Assert.Equal(1000m, history.OldPrice);
        Assert.Equal(1200m, history.NewPrice);
        Assert.Equal(1500m, history.NewCompareAtPrice);
        Assert.Equal("Ayesha Khan", history.ChangedBy);
        Assert.True(history.InsrDte > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    [Trait("Spec", "PRD-12")]
    public async Task ACollectionPlacement_IsKept_AndUnknownCollectionsAreRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var placed = await host.SendAsync(new SetCollectionsCommand(product.Id, [1, 2], product.Version, "Test Person"));

        Assert.Equal([1, 2], placed.CollectionIds);
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCollectionsCommand(product.Id, [99], placed.Version, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "PRD-4")]
    public async Task PublishingAnIncompleteProduct_NamesEveryMissingItem_AndChangesNothing()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var result = await host.SendAsync(new PublishProductCommand(product.Id, product.Version, "Test Person"));

        Assert.False(result.Published);
        var codes = result.Failures.Select(f => f.Code).ToArray();
        Assert.Equal(["NO_SELLABLE_VARIANT", "NO_IMAGE", "COMPOSITION_TOTAL", "CARE_MISSING"], codes);
        Assert.Equal("Draft", result.Product.Status);
        Assert.Equal(0, OutboxCount(postgres, "ProductPublishedEvent", product.Id));
        Assert.Contains("NO_SIZE_CHART", result.Product.Gate.Warnings);
    }

    [Fact]
    [Trait("Spec", "PRD-13")]
    [Trait("Spec", "PRD-10")]
    public async Task APartlyLabelledProduct_Publishes_WithTheOthersNotYetAvailable_AndTheEventIsWritten()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));

        var result = await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Ayesha Khan"));

        Assert.True(result.Published);
        Assert.Equal("Published", result.Product.Status);
        Assert.Equal("Ayesha Khan", result.Product.PublishedBy);
        Assert.Equal(1, result.Product.SellableVariants);
        Assert.Equal(5, result.Product.Variants.Count(v => v.Availability == "NotYetAvailable"));
        Assert.Equal(1, OutboxCount(postgres, "ProductPublishedEvent", ready.Id));
        Assert.Contains(result.Product.Path, Outbox(postgres, "ProductPublishedEvent", ready.Id), StringComparison.Ordinal);
        await using var db = postgres.NewContext();
        Assert.True(await db.PathHistory.AnyAsync(h => h.ProductId == ready.Id && h.Slug == ready.Slug));
    }

    [Fact]
    [Trait("Spec", "PRD-8")]
    public async Task ALabelledVariantWithoutAPrice_BlocksThePublish()
    {
        using var host = new CatalogHost(postgres);
        var style = await host.NewStyleAsync(retailPrice: 0m);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(style), (1, 4));

        var result = await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));

        Assert.False(result.Published);
        Assert.Equal(["VARIANT_UNPRICED"], result.Failures.Select(f => f.Code).ToArray());
    }

    [Fact]
    [Trait("Spec", "PRD-10")]
    public async Task PublishingTwice_IsIdempotent_AndWritesOneEvent()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var first = await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));

        var second = await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));

        Assert.True(second.Published);
        Assert.Equal(first.Product.PublishedAt!.Value.ToUnixTimeMilliseconds(), second.Product.PublishedAt!.Value.ToUnixTimeMilliseconds());
        Assert.Equal(1, OutboxCount(postgres, "ProductPublishedEvent", ready.Id));
    }

    [Fact]
    [Trait("Spec", "PRD-3")]
    [Trait("Spec", "PRD-10")]
    public async Task Unpublish_TakesTheProductOffSale_KeepsItsData_AndWritesTheEvent()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var published = (await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"))).Product;

        var unpublished = await host.SendAsync(new UnpublishProductCommand(ready.Id, published.Version, "Test Person"));

        Assert.Equal("Unpublished", unpublished.Status);
        Assert.Equal(6, unpublished.Variants.Count);
        Assert.Equal(1, OutboxCount(postgres, "ProductUnpublishedEvent", ready.Id));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new UnpublishProductCommand(ready.Id, unpublished.Version, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "PRD-6")]
    public async Task Archive_IsFinal_NeverDeletes_AndWritesTheEvent()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var published = (await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"))).Product;

        var archived = await host.SendAsync(new ArchiveProductCommand(ready.Id, published.Version, "Test Person"));

        Assert.Equal("Archived", archived.Status);
        Assert.Equal(1, OutboxCount(postgres, "ProductArchivedEvent", ready.Id));
        Assert.Equal("Archived", (await host.SendAsync(new ArchiveProductCommand(ready.Id, 0, "Test Person"))).Status);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new PublishProductCommand(ready.Id, archived.Version, "Test Person")));
        await using var db = postgres.NewContext();
        Assert.True(await db.Products.AnyAsync(p => p.Id == ready.Id));
    }

    [Fact]
    [Trait("Spec", "PRD-5")]
    public async Task APublishWithAStaleVersion_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => host.SendAsync(new PublishProductCommand(ready.Id, ready.Version - 1, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "PRD-5")]
    public async Task TwoPublishesAtOnce_PublishOnce()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));

        var results = await Task.WhenAll(
            Attempt(host, ready), Attempt(host, ready));

        Assert.Equal(1, OutboxCount(postgres, "ProductPublishedEvent", ready.Id));
        Assert.Contains(results, r => r == "ok");
        static async Task<string> Attempt(CatalogHost host, ProductDto ready)
        {
            try
            {
                await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));
                return "ok";
            }
            catch (Exception e) when (e is DbUpdateException)
            {
                return "conflict";
            }
        }
    }

    [Fact]
    [Trait("Spec", "PRD-3")]
    public async Task ChangesToAPublishedProduct_TellTheStorefront_WithoutAnyPrice()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var published = (await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"))).Product;

        await host.SendAsync(new SetVariantPricesCommand(ready.Id, [new VariantPriceInput(1, 4, 1999m, null)], published.Version, "Test Person"));

        Assert.Equal(1, OutboxCount(postgres, "ProductChangedEvent", ready.Id));
        Assert.DoesNotContain("1999", Outbox(postgres, "ProductChangedEvent", ready.Id), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-2")]
    public async Task OnAPublishedProduct_GenderAndCategoryChangeOnlyThroughTheUrlTab()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        var published = (await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"))).Product;

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(
            new UpdateProductContentCommand(ready.Id, published.Name, "d", 1, 3, 4, null, published.Version, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task ADraftsGenderChange_MovesItsPath_AndCollidesWithNobody()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var moved = await host.SendAsync(new UpdateProductContentCommand(product.Id, product.Name, "d", 1, 3, 4, null, product.Version, "Test Person"));

        Assert.StartsWith("/boys/dresses/", moved.Path, StringComparison.Ordinal);
        Assert.Equal(product.Slug, moved.Slug);
    }

    [Fact]
    [Trait("Spec", "PRD-11")]
    public async Task TheList_IsPaged_Ordered_Clamped_AndRunsAConstantNumberOfQueries()
    {
        using var host = new CatalogHost(postgres);
        var tag = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 5; i++)
        {
            await host.NewProductAsync(name: $"Listed {tag} {i}");
        }

        var counter = new CommandCounter();
        using var counted = new CatalogHost(postgres, counter: counter);
        counter.Reset();
        var first = await counted.SendAsync(new ListProductsQuery(Search: tag, Page: 1, PageSize: 2));
        var queriesForTwo = counter.Count;
        counter.Reset();
        var all = await counted.SendAsync(new ListProductsQuery(Search: tag, Page: 1, PageSize: 100));
        var second = await counted.SendAsync(new ListProductsQuery(Search: tag, Page: 2, PageSize: 2));
        var clamped = await counted.SendAsync(new ListProductsQuery(Search: tag, Page: 1, PageSize: 5000));

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(5, all.Items.Count);
        Assert.Equal(all.Items.Skip(2).Take(2).Select(i => i.Id), second.Items.Select(i => i.Id));
        Assert.Equal(all.Items.Select(i => i.Id).OrderDescending(), all.Items.Select(i => i.Id));
        Assert.Equal(100, clamped.PageSize);
        Assert.Equal(2, queriesForTwo);
        Assert.Equal(6, counter.Count);
    }

    [Fact]
    [Trait("Spec", "PRD-11")]
    public async Task TheList_FiltersByStatusCategoryAndSearch_AndCountsSellableVariants()
    {
        using var host = new CatalogHost(postgres);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var style = await host.NewStyleAsync();
        host.Label(style, 1, 4);
        host.Label(style, 2, 5);
        var product = await host.NewProductAsync(style, $"Filtered {tag}");
        await host.SendAsync(new RefreshVariantsFromLabellingCommand(product.Id));
        await host.NewProductAsync(name: $"Other {tag}", categoryId: 1);

        var drafts = await host.SendAsync(new ListProductsQuery(Search: tag, StatusId: (short)ProductStatus.Draft));
        var published = await host.SendAsync(new ListProductsQuery(Search: tag, StatusId: (short)ProductStatus.Published));
        var dresses = await host.SendAsync(new ListProductsQuery(Search: tag, CategoryId: 3));
        var byStyleCode = await host.SendAsync(new ListProductsQuery(Search: product.StyleCode));

        Assert.Equal(2, drafts.TotalCount);
        Assert.Equal(0, published.TotalCount);
        var row = Assert.Single(dresses.Items);
        Assert.Equal(6, row.PlannedVariants);
        Assert.Equal(2, row.SellableVariants);
        Assert.Equal(product.Id, Assert.Single(byStyleCode.Items).Id);
    }

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task GetProduct_ForAnUnknownId_IsNotFound()
    {
        using var host = new CatalogHost(postgres);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetProductQuery(long.MaxValue)));
    }

    [Fact]
    public async Task EveryWrite_RequiresTheNameOfThePersonDoingIt()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new UnpublishProductCommand(product.Id, product.Version, " ")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetCollectionsCommand(product.Id, [1], product.Version, "x\ny")));
    }

    [Fact]
    public void TheModulesCommandsAreAllMarkedAsCommands()
    {
        var commandTypes = typeof(CreateProductCommand).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Command", StringComparison.Ordinal) && t.IsAssignableTo(typeof(IBaseRequest)));

        Assert.All(commandTypes, t => Assert.True(
            t.IsAssignableTo(typeof(Romp.Modules.Catalog.Application.Abstractions.ICatalogCommand)), $"{t.Name} must be an ICatalogCommand so it runs in a transaction."));
    }
}
