using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;

namespace Romp.Modules.Shipping.Application.Bookings;

public enum CreateShipmentOutcome
{
    Created,

    /// <summary>A shipment already exists for this order: nothing changes (BKG-5).</summary>
    AlreadyExists,

    /// <summary>Orders knows no such order, or it was cancelled.</summary>
    NotEligible,
}

public sealed record CreateShipmentResult(CreateShipmentOutcome Outcome, long? ShipmentId);

/// <summary>
/// A parcel was packed and weighed: its shipment is created (BKG-1). The cash to collect is read from Orders now; the address and phone are not
/// stored here at all (BKG-7, SEC-25). A repeat does nothing (BKG-5).
/// </summary>
public sealed record CreateShipmentCommand(long OrderId, string OrderNumber, int WeightGrams) : IRequest<CreateShipmentResult>, IShippingCommand;

public sealed class CreateShipmentCommandHandler(IShippingDbContext dbContext, IOrderShippingQueries orders, TimeProvider clock)
    : IRequestHandler<CreateShipmentCommand, CreateShipmentResult>
{
    public async Task<CreateShipmentResult> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Shipments.AsNoTracking().Where(s => s.OrderId == request.OrderId).Select(s => (long?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new CreateShipmentResult(CreateShipmentOutcome.AlreadyExists, existing);
        }

        var view = await orders.FindAsync(request.OrderId, cancellationToken);
        if (view is null || view.StatusCode == "CNCL")
        {
            return new CreateShipmentResult(CreateShipmentOutcome.NotEligible, null);
        }

        var shipment = Shipment.Create(view.OrderId, view.OrderNumber, view.CashToCollect, Math.Max(1, request.WeightGrams), clock.GetUtcNow());
        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateShipmentResult(CreateShipmentOutcome.Created, shipment.Id);
    }
}

public enum BookOutcome
{
    Booked,

    /// <summary>Another worker has it, or it is not due yet: nothing was asked of a courier.</summary>
    NotDue,

    /// <summary>Every courier was unreachable: it will be tried again, or waits for staff.</summary>
    Unreachable,

    /// <summary>A courier refused the parcel: it waits for staff.</summary>
    Rejected,

    /// <summary>The shipment was already booked or finished.</summary>
    AlreadyDone,

    /// <summary>The order was cancelled meanwhile, so the shipment was cancelled.</summary>
    Cancelled,

    NotFound,
}

/// <summary>
/// Tries to book one shipment: primary courier first, the secondary when it cannot be reached (BKG-3), the manual queue when none can (BKG-4) or when
/// one refuses (BKG-6). A claim taken before any courier is asked means two workers never book the same parcel twice (BKG-5). The caller must not
/// have loaded the shipment: the claim is a conditional update, and the shipment is loaded after it.
/// </summary>
public sealed class ShipmentBooker(IShippingDbContext dbContext, IOrderShippingQueries orders, ICourierRegistry couriers, IOptions<ShippingOptions> options, TimeProvider clock)
{
    public async Task<BookOutcome> BookAsync(long shipmentId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        if (!await dbContext.ClaimForBookingAsync(shipmentId, now, now.AddMinutes(settings.ClaimMinutes), cancellationToken))
        {
            return await dbContext.Shipments.AsNoTracking().AnyAsync(s => s.Id == shipmentId, cancellationToken) ? BookOutcome.NotDue : BookOutcome.NotFound;
        }

        var shipment = await dbContext.Shipments.FirstAsync(s => s.Id == shipmentId, cancellationToken);
        if (shipment.StatusId != ShipmentStatuses.PendingBooking)
        {
            return BookOutcome.AlreadyDone;
        }

        var view = await orders.FindAsync(shipment.OrderId, cancellationToken);
        if (view is null)
        {
            shipment.RecordBookingFailure(BookingErrors.Rejected, "The order could not be found.", now, settings.RetryMinutes, settings.MaxAttempts);
            return BookOutcome.Rejected;
        }

        if (view.StatusCode == "CNCL")
        {
            shipment.Cancel(now);
            return BookOutcome.Cancelled;
        }

        var request = new BookingRequest(
            view.OrderNumber, view.RecipientName, view.RecipientPhone, view.AddressLine1, view.AddressLine2, view.City, view.Province, view.PostalCode, view.DeliveryNote,
            view.CashToCollect, shipment.WeightGrams);

        foreach (var gateway in couriers.InOrder)
        {
            var result = await TryAsync(gateway, request, settings.CourierTimeoutSeconds, cancellationToken);
            if (result.Outcome == BookingOutcome.Booked)
            {
                shipment.MarkBooked(gateway.CourierId, result.Waybill!, BookingSources.Courier, now);
                return BookOutcome.Booked;
            }

            if (result.Outcome == BookingOutcome.Rejected)
            {
                shipment.RecordBookingFailure(BookingErrors.Rejected, result.Reason, now, settings.RetryMinutes, settings.MaxAttempts);
                return BookOutcome.Rejected;
            }
        }

        shipment.RecordBookingFailure(BookingErrors.Unreachable, null, now, settings.RetryMinutes, settings.MaxAttempts);
        return BookOutcome.Unreachable;
    }

    /// <summary>A courier that throws, or takes longer than the timeout, counts as unreachable (CPT-2).</summary>
    private static async Task<BookingResult> TryAsync(ICourierGateway gateway, BookingRequest request, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return await gateway.BookAsync(request, timeout.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not OutOfMemoryException)
        {
            return BookingResult.Unreachable();
        }
    }
}

/// <summary>Books one shipment now (the retry worker, the order-packed consumer, staff "try again").</summary>
public sealed record BookShipmentCommand(long ShipmentId) : IRequest<BookOutcome>, IShippingCommand;

public sealed class BookShipmentCommandHandler(ShipmentBooker booker) : IRequestHandler<BookShipmentCommand, BookOutcome>
{
    public Task<BookOutcome> Handle(BookShipmentCommand request, CancellationToken cancellationToken) => booker.BookAsync(request.ShipmentId, cancellationToken);
}

/// <summary>The worker's pass (not a unit of work itself: each booking is): every pending shipment whose time has come is booked, one at a time, so a slow courier delays only itself (BKG-4, MBQ-2).</summary>
public sealed record BookDueShipmentsCommand(int Batch = 25) : IRequest<int>;

public sealed class BookDueShipmentsCommandHandler(IShippingDbContext dbContext, ISender sender, TimeProvider clock) : IRequestHandler<BookDueShipmentsCommand, int>
{
    public async Task<int> Handle(BookDueShipmentsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await dbContext.Shipments.AsNoTracking()
            .Where(s => s.StatusId == ShipmentStatuses.PendingBooking && !s.NeedsStaff && s.NextAttemptAt != null && s.NextAttemptAt <= now)
            .OrderBy(s => s.NextAttemptAt).Select(s => s.Id).Take(request.Batch).ToListAsync(cancellationToken);
        var booked = 0;
        foreach (var id in due)
        {
            // Each booking is its own unit of work: one that fails must not undo the others.
            if (await sender.Send(new BookShipmentCommand(id), cancellationToken) == BookOutcome.Booked)
            {
                booked++;
            }
        }

        return booked;
    }
}

/// <summary>The order was cancelled: an unbooked or uncollected shipment is cancelled, and the courier is asked to drop a booking (BKG-10).</summary>
public sealed record CancelShipmentCommand(long OrderId) : IRequest<bool>, IShippingCommand;

public sealed class CancelShipmentCommandHandler(IShippingDbContext dbContext, ICourierRegistry couriers, TimeProvider clock) : IRequestHandler<CancelShipmentCommand, bool>
{
    public async Task<bool> Handle(CancelShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipment = await dbContext.Shipments.FirstOrDefaultAsync(s => s.OrderId == request.OrderId, cancellationToken);
        if (shipment is null)
        {
            return false;
        }

        var waybill = shipment.Waybill;
        var courierId = shipment.CourierId;
        var (cancelled, wasBooked) = shipment.Cancel(clock.GetUtcNow());
        if (cancelled && wasBooked && waybill is not null && courierId is { } id && couriers.Find(id) is { } gateway)
        {
            // The courier is told when the port can; if it cannot, the shipment stays cancelled here and staff see it on the manifest and in the history.
            try
            {
                await gateway.CancelAsync(waybill, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not OutOfMemoryException)
            {
                // An unreachable courier is not a reason to keep the booking alive here.
            }
        }

        return cancelled;
    }
}
