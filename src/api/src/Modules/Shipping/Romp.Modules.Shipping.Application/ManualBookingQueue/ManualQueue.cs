using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Domain;

namespace Romp.Modules.Shipping.Application.ManualBookingQueue;

/// <summary>One parcel waiting for a booking: who it is, why it is waiting, how often it was tried and how long it has waited (MBQ-1).</summary>
public sealed record QueueRowDto(
    long Id, long OrderId, string OrderNumber, string? Reason, string? Note, int Attempts, bool NeedsStaff, int WaitingMinutes, DateTimeOffset? NextAttemptAt, decimal CashToCollect);

/// <summary>The manual booking queue (MBQ-1): every shipment still pending, oldest first, paged. One count and one page query.</summary>
public sealed record ListManualQueueQuery(bool NeedsStaffOnly = false, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<QueueRowDto>>;

public sealed class ListManualQueueQueryHandler(IShippingDbContext dbContext, TimeProvider clock) : IRequestHandler<ListManualQueueQuery, PagedResult<QueueRowDto>>
{
    public async Task<PagedResult<QueueRowDto>> Handle(ListManualQueueQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Shipments.AsNoTracking().Where(s => s.StatusId == ShipmentStatuses.PendingBooking);
        if (request.NeedsStaffOnly)
        {
            query = query.Where(s => s.NeedsStaff);
        }

        var page = await query.OrderBy(s => s.InsrDte).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.OrderId, s.OrderNumber, s.LastError, s.LastNote, s.Attempts, s.NeedsStaff, s.InsrDte, s.NextAttemptAt, s.CashToCollect })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        var now = clock.GetUtcNow();
        return new PagedResult<QueueRowDto>(
            [.. page.Items.Select(s => new QueueRowDto(s.Id, s.OrderId, s.OrderNumber, s.LastError, s.LastNote, s.Attempts, s.NeedsStaff, (int)Math.Max(0, (now - s.InsrDte).TotalMinutes), s.NextAttemptAt, s.CashToCollect))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>Staff pressed "try again now" (MBQ-3): the shipment becomes due at once. The booking itself is a separate step, so a repeat press does nothing more.</summary>
public sealed record RetryNowCommand(long ShipmentId, string Staff) : IRequest<bool>, IShippingCommand;

public sealed class RetryNowCommandHandler(IShippingDbContext dbContext, TimeProvider clock) : IRequestHandler<RetryNowCommand, bool>
{
    public async Task<bool> Handle(RetryNowCommand request, CancellationToken cancellationToken)
    {
        EnsureStaff(request.Staff);
        var shipment = await dbContext.Shipments.FirstOrDefaultAsync(s => s.Id == request.ShipmentId, cancellationToken);
        return shipment is not null && shipment.RequestRetry(clock.GetUtcNow());
    }

    internal static void EnsureStaff(string staff)
    {
        if (string.IsNullOrWhiteSpace(staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }
    }
}

/// <summary>
/// Staff got a waybill from the courier by phone (MBQ-4): the shipment is booked with it and the source is recorded as manual. A waybill that another
/// shipment already holds is refused (MBQ-5).
/// </summary>
public sealed record EnterManualWaybillCommand(long ShipmentId, string CourierCode, string Waybill, string Staff) : IRequest<bool>, IShippingCommand;

public sealed class EnterManualWaybillCommandHandler(IShippingDbContext dbContext, TimeProvider clock) : IRequestHandler<EnterManualWaybillCommand, bool>
{
    public async Task<bool> Handle(EnterManualWaybillCommand request, CancellationToken cancellationToken)
    {
        RetryNowCommandHandler.EnsureStaff(request.Staff);
        var courier = Couriers.FromCode(request.CourierCode) ?? throw new DomainException("Choose the courier that gave the waybill.");
        var waybill = (request.Waybill ?? string.Empty).Trim().ToUpperInvariant();
        var shipment = await dbContext.Shipments.FirstOrDefaultAsync(s => s.Id == request.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return false;
        }

        if (await dbContext.Shipments.AsNoTracking().AnyAsync(s => s.Waybill == waybill && s.Id != shipment.Id, cancellationToken))
        {
            throw new DomainException("That waybill is already on another parcel.");
        }

        if (!shipment.MarkBooked(courier, waybill, BookingSources.Manual, clock.GetUtcNow()))
        {
            throw new DomainException("This parcel is already booked.");
        }

        return true;
    }
}
