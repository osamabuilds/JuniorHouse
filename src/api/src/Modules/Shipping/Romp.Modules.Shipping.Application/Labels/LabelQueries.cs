using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Domain;

namespace Romp.Modules.Shipping.Application.Labels;

/// <summary>
/// The combined shipping label (LBL-1 to LBL-5): recipient, full address and phone, the order number, the courier and its waybill as text and as a
/// Code 128 barcode, and the cash to collect or "Paid". It holds the customer's phone and address, so it is built from Orders when asked, for staff
/// only, and never stored or logged (LBL-7, SEC-25).
/// </summary>
public sealed record LabelDto(
    long ShipmentId, string OrderNumber, string CourierName, string Waybill, string BarcodeModules, string RecipientName, string Phone, string AddressLine1,
    string? AddressLine2, string City, string Province, string? PostalCode, string? DeliveryNote, bool IsCashOnDelivery, decimal CashToCollect, int WeightGrams,
    bool IsReprint, int PrintCount);

public sealed record GetLabelQuery(long ShipmentId) : IRequest<LabelDto?>;

public sealed class GetLabelQueryHandler(IShippingDbContext dbContext, IOrderShippingQueries orders) : IRequestHandler<GetLabelQuery, LabelDto?>
{
    public async Task<LabelDto?> Handle(GetLabelQuery request, CancellationToken cancellationToken)
    {
        var shipment = await dbContext.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return null;
        }

        // No label before a waybill (LBL-2): the courier's number cannot be invented.
        if (shipment.Waybill is null || shipment.CourierId is null || shipment.StatusId == ShipmentStatuses.Cancelled)
        {
            throw new DomainException("This parcel has no waybill yet, so it cannot be labelled.");
        }

        var view = await orders.FindAsync(shipment.OrderId, cancellationToken);
        if (view is null)
        {
            return null;
        }

        return new LabelDto(
            shipment.Id, view.OrderNumber, Couriers.NameOf(shipment.CourierId.Value), shipment.Waybill, Code128.Modules(shipment.Waybill), view.RecipientName, view.RecipientPhone,
            view.AddressLine1, view.AddressLine2, view.City, view.Province, view.PostalCode, view.DeliveryNote, view.IsCashOnDelivery, view.CashToCollect, shipment.WeightGrams,
            shipment.LabelPrintCount > 0, shipment.LabelPrintCount);
    }
}

/// <summary>The label was sent to the printer: the next print is a reprint (LBL-5). The shipment itself is otherwise unchanged.</summary>
public sealed record MarkLabelPrintedCommand(long ShipmentId) : IRequest<bool>, IShippingCommand;

public sealed class MarkLabelPrintedCommandHandler(IShippingDbContext dbContext) : IRequestHandler<MarkLabelPrintedCommand, bool>
{
    public async Task<bool> Handle(MarkLabelPrintedCommand request, CancellationToken cancellationToken)
    {
        var shipment = await dbContext.Shipments.FirstOrDefaultAsync(s => s.Id == request.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return false;
        }

        shipment.RecordLabelPrinted();
        return true;
    }
}
