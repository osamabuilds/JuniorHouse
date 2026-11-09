using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Captcha;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;

namespace Romp.Modules.Shipping.Application.Tracking;

public enum PushOutcome
{
    /// <summary>The status was recorded (it may or may not have moved the shipment).</summary>
    Accepted,

    /// <summary>A bad signature, an unknown courier, an unknown waybill or a body that is not a status: one answer for all (TRK-2, SEC-07).</summary>
    Rejected,
}

/// <summary>
/// A courier pushed a status (TRK-1 to TRK-4, TRK-11). The signature is checked over the raw body by the courier's gateway before anything is read;
/// anything wrong, in any way, is the same refusal. The same courier event twice changes nothing and still answers success (TRK-3).
/// </summary>
public sealed record ApplyCourierPushCommand(string CourierCode, string Body, string? Signature) : IRequest<PushOutcome>, IShippingCommand;

public sealed class ApplyCourierPushCommandHandler(IShippingDbContext dbContext, ICourierRegistry couriers) : IRequestHandler<ApplyCourierPushCommand, PushOutcome>
{
    public async Task<PushOutcome> Handle(ApplyCourierPushCommand request, CancellationToken cancellationToken)
    {
        if (couriers.FindByCode(request.CourierCode) is not { } gateway || gateway.ParsePush(request.Body, request.Signature) is not { } push)
        {
            return PushOutcome.Rejected;
        }

        var shipment = await dbContext.Shipments.Include(s => s.Events).FirstOrDefaultAsync(s => s.Waybill == push.Waybill && s.CourierId == gateway.CourierId, cancellationToken);
        if (shipment is null || ShipmentStatuses.FromCode(push.Status.StatusCode) is not { } status)
        {
            return PushOutcome.Rejected;
        }

        try
        {
            shipment.Apply(status, push.Status.CourierEventId, push.Status.At, EventSources.Push);
        }
        catch (Romp.BuildingBlocks.Domain.DomainException)
        {
            return PushOutcome.Rejected;
        }

        return PushOutcome.Accepted;
    }
}

/// <summary>
/// The polling fallback (TRK-6): for a courier that does not push, every shipment on its way that has not been asked about for
/// <c>Shipping:PollMinutes</c> is asked, and the answer is applied exactly like a push. Returns how many shipments moved.
/// </summary>
public sealed record PollShipmentsCommand(int Batch = 50) : IRequest<int>, IShippingCommand;

public sealed class PollShipmentsCommandHandler(IShippingDbContext dbContext, ICourierRegistry couriers, IOptions<ShippingOptions> options, TimeProvider clock)
    : IRequestHandler<PollShipmentsCommand, int>
{
    public async Task<int> Handle(PollShipmentsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var dueBefore = now.AddMinutes(-options.Value.PollMinutes);
        var pollable = couriers.InOrder.Where(c => !c.PushesStatus).Select(c => c.CourierId).ToList();
        if (pollable.Count == 0)
        {
            return 0;
        }

        var shipments = await dbContext.Shipments.Include(s => s.Events)
            .Where(s => s.CourierId != null && pollable.Contains(s.CourierId.Value) && s.Waybill != null
                && (s.StatusId == ShipmentStatuses.Booked || s.StatusId == ShipmentStatuses.PickedUp || s.StatusId == ShipmentStatuses.InTransit
                    || s.StatusId == ShipmentStatuses.OutForDelivery || s.StatusId == ShipmentStatuses.FailedAttempt)
                && (s.PolledAt == null || s.PolledAt <= dueBefore))
            .OrderBy(s => s.PolledAt).Take(request.Batch).ToListAsync(cancellationToken);

        var moved = 0;
        foreach (var shipment in shipments)
        {
            var gateway = couriers.Find(shipment.CourierId!.Value);
            shipment.RecordPolled(now);
            if (gateway is null)
            {
                continue;
            }

            CourierStatus? answer;
            try
            {
                answer = await gateway.StatusAsync(shipment.Waybill!, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not OutOfMemoryException)
            {
                continue;
            }

            if (answer is not null && ShipmentStatuses.FromCode(answer.StatusCode) is { } status && status != ShipmentStatuses.Booked
                && shipment.Apply(status, answer.CourierEventId, answer.At, EventSources.Poll) == ApplyOutcome.Moved)
            {
                moved++;
            }
        }

        return moved;
    }
}

public sealed record ShipmentRowDto(long Id, long OrderId, string OrderNumber, string Status, string StatusName, string? CourierName, string? Waybill, DateTimeOffset StatusAt, int FailedAttempts, bool NeedsStaff, decimal CashToCollect);

public sealed record ShipmentEventDto(string Status, string StatusName, string CourierEventId, DateTimeOffset OccurredAt, string Source);

public sealed record ShipmentDetailDto(ShipmentRowDto Row, string? BookingSource, DateTimeOffset? BookedAt, int Attempts, string? LastError, string? LastNote, int WeightGrams, long? ManifestId, int LabelPrints, IReadOnlyList<ShipmentEventDto> Events);

/// <summary>The shipments, newest first, paged, optionally by status (TRK-13). One count and one page query.</summary>
public sealed record ListShipmentsQuery(string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<ShipmentRowDto>>;

public sealed class ListShipmentsQueryHandler(IShippingDbContext dbContext) : IRequestHandler<ListShipmentsQuery, PagedResult<ShipmentRowDto>>
{
    public async Task<PagedResult<ShipmentRowDto>> Handle(ListShipmentsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Shipments.AsNoTracking();
        if (ShipmentStatuses.FromCode(request.Status) is { } status)
        {
            query = query.Where(s => s.StatusId == status);
        }

        var page = await query.OrderByDescending(s => s.Id)
            .Select(s => new { s.Id, s.OrderId, s.OrderNumber, s.StatusId, s.CourierId, s.Waybill, s.StatusAt, s.FailedAttempts, s.NeedsStaff, s.CashToCollect })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<ShipmentRowDto>(
            [.. page.Items.Select(s => new ShipmentRowDto(s.Id, s.OrderId, s.OrderNumber, ShipmentStatuses.CodeOf(s.StatusId), ShipmentStatuses.NameOf(s.StatusId), s.CourierId is { } c ? Couriers.NameOf(c) : null, s.Waybill, s.StatusAt, s.FailedAttempts, s.NeedsStaff, s.CashToCollect))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One shipment with its full timeline and booking history, for staff (TRK-13).</summary>
public sealed record GetShipmentQuery(long ShipmentId) : IRequest<ShipmentDetailDto?>;

public sealed class GetShipmentQueryHandler(IShippingDbContext dbContext) : IRequestHandler<GetShipmentQuery, ShipmentDetailDto?>
{
    public async Task<ShipmentDetailDto?> Handle(GetShipmentQuery request, CancellationToken cancellationToken)
    {
        var s = await dbContext.Shipments.AsNoTracking().Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == request.ShipmentId, cancellationToken);
        if (s is null)
        {
            return null;
        }

        var row = new ShipmentRowDto(s.Id, s.OrderId, s.OrderNumber, ShipmentStatuses.CodeOf(s.StatusId), ShipmentStatuses.NameOf(s.StatusId), s.CourierId is { } c ? Couriers.NameOf(c) : null, s.Waybill, s.StatusAt, s.FailedAttempts, s.NeedsStaff, s.CashToCollect);
        return new ShipmentDetailDto(
            row, s.BookingSourceId == BookingSources.Manual ? "MANUAL" : s.BookingSourceId == BookingSources.Courier ? "COURIER" : null, s.BookedAt, s.Attempts, s.LastError, s.LastNote, s.WeightGrams, s.ManifestId, s.LabelPrintCount,
            [.. s.Events.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => new ShipmentEventDto(ShipmentStatuses.CodeOf(e.StatusId), ShipmentStatuses.NameOf(e.StatusId), e.CourierEventId, e.OccurredAt, e.SourceId == EventSources.Push ? "PUSH" : e.SourceId == EventSources.Poll ? "POLL" : "SYSTEM"))]);
    }
}

public enum TrackLookupStatus
{
    Found,

    /// <summary>One answer for a waybill that does not exist, is malformed or is wrong (TRK-10, SEC-07).</summary>
    NotFound,
    CaptchaRequired,
}

public sealed record TrackLookupResult(TrackLookupStatus Status, ShipmentTrackingView? Tracking = null);

/// <summary>Counts failed public look-ups per client address, so a CAPTCHA can be asked for after a few (SEC-06). Implemented in Infrastructure.</summary>
public interface ITrackingThrottle
{
    bool NeedsCaptcha(string? clientIp);

    void RecordFailure(string? clientIp);
}

/// <summary>
/// A tracking number alone shows the courier status timeline and nothing else (TRK-9, OLK-8). A wrong, malformed or unknown number is one answer
/// (TRK-10, SEC-07); after a few failures from one address a CAPTCHA is required (SEC-06). The endpoint is also rate limited.
/// </summary>
public sealed record TrackByWaybillQuery(string? Waybill, string? CaptchaToken, string? ClientIp) : IRequest<TrackLookupResult>;

public sealed class TrackByWaybillQueryHandler(IShipmentTrackingQueries tracking, ITrackingThrottle throttle, ICaptchaVerifier captcha) : IRequestHandler<TrackByWaybillQuery, TrackLookupResult>
{
    public async Task<TrackLookupResult> Handle(TrackByWaybillQuery request, CancellationToken cancellationToken)
    {
        if (throttle.NeedsCaptcha(request.ClientIp) && !await captcha.VerifyAsync(request.CaptchaToken, request.ClientIp, cancellationToken))
        {
            return new TrackLookupResult(TrackLookupStatus.CaptchaRequired);
        }

        var waybill = (request.Waybill ?? string.Empty).Trim().ToUpperInvariant();
        var wellFormed = waybill.Length is > 0 and <= Shipment.MaxWaybillLength && waybill.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
        var view = wellFormed ? await tracking.FindByWaybillAsync(waybill, cancellationToken) : null;
        if (view is null)
        {
            throttle.RecordFailure(request.ClientIp);
            return new TrackLookupResult(TrackLookupStatus.NotFound);
        }

        return new TrackLookupResult(TrackLookupStatus.Found, view);
    }
}
