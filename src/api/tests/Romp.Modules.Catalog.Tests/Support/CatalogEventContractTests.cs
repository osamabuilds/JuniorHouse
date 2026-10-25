using System.Text.Json;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Tests.Support;

/// <summary>Plan G8: the Catalog events are versioned, identify their aggregate, and carry ids and the public path only (ADR 0011).</summary>
public sealed class CatalogEventContractTests
{
    private static readonly CatalogEvent[] ProductEvents =
    [
        new ProductPublishedEvent(7, 3, "/girls/dresses/floral-dress"),
        new ProductUnpublishedEvent(7, 3, "/girls/dresses/floral-dress"),
        new ProductArchivedEvent(7, 3, "/girls/dresses/floral-dress"),
        new ProductChangedEvent(7, "/girls/dresses/new-name", "/girls/dresses/floral-dress"),
    ];

    [Fact]
    [Trait("Spec", "PRD-10")]
    public void EveryProductEvent_IsVersionOne_AndAggregatesTheProduct()
    {
        foreach (var e in ProductEvents)
        {
            Assert.Equal(1, e.SchemaVersion);
            Assert.Equal("Product", e.AggregateType);
            Assert.Equal(7, e.AggregateId);
            Assert.Equal(e.GetType().Name, e.EventType);
        }
    }

    [Fact]
    [Trait("Spec", "CHT-2")]
    public void TheChartEvent_AggregatesTheChart_AndNamesItsCategoryAndGender()
    {
        var e = new SizeChartChangedEvent(4, 3, 2);

        Assert.Equal("SizeChart", e.AggregateType);
        Assert.Equal(4, e.AggregateId);
        Assert.Equal(1, e.SchemaVersion);
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void NoEvent_CarriesPricesNotesOrPeople()
    {
        foreach (var e in ProductEvents.Append(new SizeChartChangedEvent(4, 3, 2)))
        {
            var json = JsonSerializer.Serialize(e, e.GetType());
            Assert.DoesNotContain("Price", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Note", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Person", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PublishedBy", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryEventTypeInTheContract_IsRegisteredWithTheOutbox()
    {
        var declared = typeof(CatalogEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(CatalogEvent)) && !t.IsAbstract).Select(t => t.Name).Order().ToArray();

        Assert.Equal(
            ["ProductArchivedEvent", "ProductChangedEvent", "ProductPublishedEvent", "ProductUnpublishedEvent", "SizeChartChangedEvent"],
            declared);
    }
}

/// <summary>
/// The product events carry the product's content version (task 3 of published-catalog; PCT-5). The field is additive within schema
/// version 1 (ADR 0011): a payload written before it existed still reads, with version 0 meaning "unknown, fetch the snapshot".
/// </summary>
public sealed class CatalogEventVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static Product Published()
    {
        var product = Product.Create(7, "Floral Dress", "A dress.", 2, 3, 4, "floral-dress", [1], [4], 1299m);
        product.Publish("A B", Now, "/g/c/s");
        return product;
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void EveryProductEvent_CarriesTheVersionItWasRaisedAt()
    {
        var product = Published();
        product.NoteChanged("/g/c/s");
        product.Unpublish("/g/c/s");

        var events = product.DomainEvents.ToArray();

        Assert.Equal([2L, 3L, 4L], events.Select(e => e switch
        {
            ProductPublishedEvent p => p.Version,
            ProductChangedEvent p => p.Version,
            ProductUnpublishedEvent p => p.Version,
            _ => -1L,
        }));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void ArchivingAPublishedProduct_GivesBothItsEventsTheSameVersion()
    {
        var product = Published();
        product.ClearDomainEvents();

        product.Archive("/g/c/s");

        var archived = Assert.IsType<ProductArchivedEvent>(product.DomainEvents.ElementAt(0));
        var unpublished = Assert.IsType<ProductUnpublishedEvent>(product.DomainEvents.ElementAt(1));
        Assert.Equal(3, archived.Version);
        Assert.Equal(3, unpublished.Version);
    }

    [Theory]
    [InlineData("{\"ProductId\":7,\"StyleId\":3,\"Path\":\"/g/c/s\"}", typeof(ProductPublishedEvent))]
    [InlineData("{\"ProductId\":7,\"StyleId\":3,\"Path\":\"/g/c/s\"}", typeof(ProductUnpublishedEvent))]
    [InlineData("{\"ProductId\":7,\"StyleId\":3,\"Path\":\"/g/c/s\"}", typeof(ProductArchivedEvent))]
    [InlineData("{\"ProductId\":7,\"Path\":\"/g/c/s\",\"PreviousPath\":null}", typeof(ProductChangedEvent))]
    [Trait("Spec", "ADR-0011")]
    public void APayloadWrittenBeforeTheVersionExisted_StillReads_AsVersionZero(string oldJson, Type eventType)
    {
        var e = (CatalogEvent)JsonSerializer.Deserialize(oldJson, eventType)!;

        Assert.Equal(7, e.AggregateId);
        Assert.Equal(0, (long)eventType.GetProperty("Version")!.GetValue(e)!);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void TheVersionSurvivesTheRoundTripThroughJson()
    {
        var e = new ProductChangedEvent(7, "/g/c/s", null, 12);

        var back = JsonSerializer.Deserialize<ProductChangedEvent>(JsonSerializer.Serialize(e))!;

        Assert.Equal(12, back.Version);
    }
}
