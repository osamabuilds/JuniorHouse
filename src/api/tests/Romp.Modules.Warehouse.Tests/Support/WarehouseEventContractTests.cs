using System.Text.Json;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Warehouse.Tests.Support;

/// <summary>Plan G6: the three warehouse events are versioned, identify their aggregate, and carry ids, SKU, GTIN and quantities only (ADR 0011).</summary>
public sealed class WarehouseEventContractTests
{
    private static readonly WarehouseEvent[] Events =
    [
        new StockPutawayConfirmedEvent(7, 3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000017", "ACCEPTED", 10, 10),
        new StockLevelChangedEvent(7, 3, 2, 4, "ROMP-RMP-RED-2-3Y", "0200000000017", 12, 12, 10, 2, "PUTAWAY"),
        new StockAlertRaisedEvent(7, "ROMP-RMP-RED-2-3Y", "LOW", 4, 5),
    ];

    [Fact]
    [Trait("Spec", "LED-4")]
    public void EveryEvent_IsVersionOne_AndAggregatesTheVariant()
    {
        foreach (var e in Events)
        {
            Assert.Equal(1, e.SchemaVersion);
            Assert.Equal("Variant", e.AggregateType);
            Assert.Equal(7, e.AggregateId);
            Assert.Equal(e.GetType().Name, e.EventType);
        }
    }

    [Fact]
    [Trait("Spec", "LED-4")]
    public void TheStockLevelEvent_ExposesOnHandAvailableAndTheGradeSplit()
    {
        var e = (StockLevelChangedEvent)Events[1];

        Assert.Equal(e.OnHand, e.OnHandAccepted + e.OnHandConcession);
        Assert.Equal(e.OnHand, e.Available);
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void NoEvent_CarriesNamesNotesOrPeople()
    {
        foreach (var e in Events)
        {
            var json = JsonSerializer.Serialize(e, e.GetType());
            Assert.DoesNotContain("Note", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Actor", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Person", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
