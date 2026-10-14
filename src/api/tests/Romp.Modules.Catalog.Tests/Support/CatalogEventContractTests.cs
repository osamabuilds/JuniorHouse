using System.Text.Json;
using Romp.Modules.Catalog.Contracts;

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
