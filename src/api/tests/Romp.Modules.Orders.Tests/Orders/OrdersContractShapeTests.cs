using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class OrdersContractShapeTests
{
    private static readonly string[] Forbidden = ["name", "email", "phone", "mobile", "address", "note", "line", "postcode", "contact", "recipient"];

    [Fact]
    [Trait("Spec", "SEC-25")]
    public void NoOrdersEvent_CarriesAnyPersonalField()
    {
        var events = typeof(OrdersEvent).Assembly.GetTypes().Where(t => typeof(OrdersEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        Assert.NotEmpty(events);
        foreach (var type in events)
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain(Forbidden, f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    public void EveryEvent_NamesTheOrderAndIsVersionOne()
    {
        var placed = new OrderPlacedEvent(5, "RMP-2026-00001-4", "key", "COD", "9");

        Assert.Equal(("Order", 5L, (short)1, "OrderPlacedEvent"), (placed.AggregateType, placed.AggregateId, placed.SchemaVersion, placed.EventType));
    }
}
