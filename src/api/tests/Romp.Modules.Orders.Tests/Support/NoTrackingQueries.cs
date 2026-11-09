using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Orders.Tests.Support;

/// <summary>A Shipping that has no shipment for any order or waybill: the tracking block is empty.</summary>
internal sealed class NoTrackingQueries : IShipmentTrackingQueries
{
    public Task<ShipmentTrackingView?> FindByOrderAsync(long orderId, CancellationToken cancellationToken) => Task.FromResult<ShipmentTrackingView?>(null);

    public Task<ShipmentTrackingView?> FindByWaybillAsync(string waybill, CancellationToken cancellationToken) => Task.FromResult<ShipmentTrackingView?>(null);
}

/// <summary>A Shipping with a scripted answer, for tests of the tracking block.</summary>
internal sealed class FakeTrackingQueries : IShipmentTrackingQueries
{
    public ShipmentTrackingView? View { get; set; }

    public Task<ShipmentTrackingView?> FindByOrderAsync(long orderId, CancellationToken cancellationToken) => Task.FromResult(View);

    public Task<ShipmentTrackingView?> FindByWaybillAsync(string waybill, CancellationToken cancellationToken) => Task.FromResult(View);
}
