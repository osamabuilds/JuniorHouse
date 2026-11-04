using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Application.OrderQueue;

/// <summary>Display names for the stable status codes, so the admin never shows a code on its own.</summary>
public static class OrderLabels
{
    public static string Status(short id) => id switch
    {
        OrderStatuses.PendingConfirmation => "Pending confirmation",
        OrderStatuses.Confirmed => "Confirmed",
        OrderStatuses.PaymentPending => "Payment pending",
        OrderStatuses.Paid => "Paid",
        OrderStatuses.Cancelled => "Cancelled",
        OrderStatuses.Dispatched => "Dispatched",
        OrderStatuses.Delivered => "Delivered",
        _ => "Unknown",
    };

    public static string Method(short id) => id switch
    {
        PaymentMethods.CashOnDelivery => "Cash on Delivery",
        PaymentMethods.Easypaisa => "Easypaisa",
        PaymentMethods.JazzCash => "JazzCash",
        PaymentMethods.Card => "Card",
        _ => "Unknown",
    };

    public static string Outcome(short id) => id switch
    {
        CodOutcomes.Confirmed => "Confirmed by staff",
        CodOutcomes.NoAnswer => "No answer",
        CodOutcomes.Refused => "Refused",
        CodOutcomes.ConfirmedByCustomerOnWhatsApp => "Confirmed by the customer on WhatsApp",
        CodOutcomes.ConfirmedByCustomerOnPage => "Confirmed by the customer on the page",
        CodOutcomes.CancelledByCustomerOnWhatsApp => "Cancelled by the customer on WhatsApp",
        CodOutcomes.CancelledByCustomerOnPage => "Cancelled by the customer on the page",
        _ => "Unknown",
    };

    public static string Reason(short id) => id switch
    {
        CancelReasons.Staff => "Cancelled by staff",
        CancelReasons.CodNoAnswer => "No answer to the confirmation calls",
        CancelReasons.CodRefused => "The customer refused the order",
        CancelReasons.Customer => "Cancelled by the customer",
        CancelReasons.PaymentFailed => "The payment failed",
        CancelReasons.PaymentExpired => "The payment window passed",
        _ => "Unknown",
    };

    public static string Actor(short id) => id switch
    {
        ActorKinds.Staff => "Staff",
        ActorKinds.Customer => "Customer",
        ActorKinds.System => "System",
        ActorKinds.Gateway => "Payment gateway",
        _ => "Unknown",
    };
}

public sealed record OrderListItemDto(
    long Id, string OrderNumber, short StatusId, string StatusName, string MethodName, decimal Total, DateTimeOffset PlacedAt, string ContactName, string CityName,
    int ItemCount, DateTimeOffset? CodNextDueAt, DateTimeOffset? CodAutoCancelAt, bool IsFirstOrder, bool IsHighValue, bool IsRefusalFlagged);

/// <summary>
/// The staff list of orders (OQ-1, OQ-7, OQ-9): filters by status, method, dates, "cash on delivery call due now" and a search by order number
/// or mobile. Paged, newest first, one query for the page (the line count is a subquery, no N+1).
/// </summary>
public sealed record ListOrdersQuery(
    short? StatusId = null,
    short? MethodId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    bool CallDueNow = false,
    bool FlaggedOnly = false,
    string? Search = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<OrderListItemDto>>;

public sealed class ListOrdersQueryHandler(IOrdersDbContext dbContext, TimeProvider clock) : IRequestHandler<ListOrdersQuery, PagedResult<OrderListItemDto>>
{
    public async Task<PagedResult<OrderListItemDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var orders = dbContext.Orders.AsNoTracking().AsQueryable();
        if (request.StatusId is { } status)
        {
            orders = orders.Where(o => o.StatusId == status);
        }

        if (request.MethodId is { } method)
        {
            orders = orders.Where(o => o.PaymentMethodId == method);
        }

        if (request.From is { } from)
        {
            orders = orders.Where(o => o.PlacedAt >= from);
        }

        if (request.To is { } to)
        {
            orders = orders.Where(o => o.PlacedAt < to);
        }

        if (request.CallDueNow)
        {
            orders = orders.Where(o => o.StatusId == OrderStatuses.PendingConfirmation && o.CodNextDueAt != null && o.CodNextDueAt <= now);
        }

        if (request.FlaggedOnly)
        {
            orders = orders.Where(o => o.IsRefusalFlagged || o.IsHighValue || o.IsFirstOrder);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim();
            var number = OrderNumbers.Normalise(text);
            PhoneNumber.TryNormalise(text, out var phone);

            // A name search is a plain "contains" (case-sensitive, as typed), with the wildcard characters of the text made literal.
            var pattern = "%" + text.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal) + "%";
            orders = orders.Where(o => (number != null && o.OrderNumber == number) || (phone != null && phone != string.Empty && o.ContactMobile == phone) || EF.Functions.Like(o.ContactName, pattern));
        }

        var page = await orders
            .OrderByDescending(o => o.Id)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.StatusId, o.PaymentMethodId, o.Total, o.PlacedAt, o.ContactName, o.CityName,
                ItemCount = o.Lines.Sum(l => l.Quantity), o.CodNextDueAt, o.CodAutoCancelAt, o.IsFirstOrder, o.IsHighValue, o.IsRefusalFlagged,
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var items = page.Items.Select(o => new OrderListItemDto(
            o.Id, o.OrderNumber, o.StatusId, OrderLabels.Status(o.StatusId), OrderLabels.Method(o.PaymentMethodId), o.Total, o.PlacedAt, o.ContactName, o.CityName,
            o.ItemCount, o.CodNextDueAt, o.CodAutoCancelAt, o.IsFirstOrder, o.IsHighValue, o.IsRefusalFlagged)).ToList();
        return new PagedResult<OrderListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed record OrderLineDetailDto(long VariantId, string Name, string? Sku, string Colour, string Size, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderHistoryDto(string FromStatus, string ToStatus, string? Reason, string Actor, string? ActorRef, string? Note, DateTimeOffset At);

public sealed record CodAttemptDto(int Number, string Outcome, string Actor, string? ActorRef, string? Note, DateTimeOffset At);

/// <summary>
/// One order as staff see it (OQ-1, OQ-6): contact, delivery address, lines, amounts, history and call attempts. The phone and address are
/// shown to staff with <c>orders.view</c> only (they must call and pack) and are never written to a log (SEC-25).
/// </summary>
public sealed record OrderDetailDto(
    long Id,
    string OrderNumber,
    short StatusId,
    string StatusName,
    string MethodName,
    bool IsCashOnDelivery,
    DateTimeOffset PlacedAt,
    string ContactName,
    string? ContactEmail,
    string ContactMobile,
    bool WhatsAppOptIn,
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string CityName,
    string ProvinceName,
    string? PostalCode,
    string? DeliveryNote,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    decimal Total,
    string? DiscountCode,
    bool IsFirstOrder,
    bool IsHighValue,
    bool IsRefusalFlagged,
    DateTimeOffset? CodNextDueAt,
    DateTimeOffset? CodAutoCancelAt,
    int CodAttemptCount,
    bool CanRecordCodOutcome,
    bool CanCancel,
    IReadOnlyList<OrderLineDetailDto> Lines,
    IReadOnlyList<OrderHistoryDto> History,
    IReadOnlyList<CodAttemptDto> CodAttempts);

public sealed record GetOrderQuery(long Id) : IRequest<OrderDetailDto?>;

public sealed class GetOrderQueryHandler(IOrdersDbContext dbContext) : IRequestHandler<GetOrderQuery, OrderDetailDto?>
{
    public async Task<OrderDetailDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        // One query for the order with its three child lists (split into a fixed small number of statements by EF, never one per row).
        var o = await dbContext.Orders.AsNoTracking().AsSplitQuery().Include(x => x.Lines).Include(x => x.History).Include(x => x.CodAttempts)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (o is null)
        {
            return null;
        }

        var cod = PaymentMethods.IsCashOnDelivery(o.PaymentMethodId);
        return new OrderDetailDto(
            o.Id, o.OrderNumber, o.StatusId, OrderLabels.Status(o.StatusId), OrderLabels.Method(o.PaymentMethodId), cod, o.PlacedAt,
            o.ContactName, o.ContactEmail, o.ContactMobile, o.WhatsAppOptIn, o.RecipientName, o.RecipientPhone,
            string.IsNullOrWhiteSpace(o.AddressLine2) ? o.AddressLine1 : $"{o.AddressLine1}, {o.AddressLine2}", o.CityName, o.ProvinceName, o.PostalCode, o.DeliveryNote,
            o.Subtotal, o.Discount, o.Shipping, o.Total, o.DiscountCode, o.IsFirstOrder, o.IsHighValue, o.IsRefusalFlagged,
            o.CodNextDueAt, o.CodAutoCancelAt, o.CodAttemptCount,
            CanRecordCodOutcome: cod && o.StatusId == OrderStatuses.PendingConfirmation,
            CanCancel: o.StatusId is not (OrderStatuses.Cancelled or OrderStatuses.Dispatched or OrderStatuses.Delivered),
            [.. o.Lines.OrderBy(l => l.Id).Select(l => new OrderLineDetailDto(l.VariantId, l.Name, l.Sku, l.Colour, l.Size, l.UnitPrice, l.Quantity, l.LineTotal))],
            [.. o.History.OrderBy(h => h.Id).Select(h => new OrderHistoryDto(
                h.FromStatusId is { } f ? OrderLabels.Status(f) : "Placed", OrderLabels.Status(h.ToStatusId), h.ReasonId is { } r ? OrderLabels.Reason(r) : null,
                OrderLabels.Actor(h.ActorKindId), h.ActorRef, h.Note, h.At))],
            [.. o.CodAttempts.OrderBy(a => a.Number).Select(a => new CodAttemptDto(a.Number, OrderLabels.Outcome(a.OutcomeId), OrderLabels.Actor(a.ActorKindId), a.ActorRef, a.Note, a.At))]);
    }
}

/// <summary>Staff clear the refusal flag on a mobile number after review (COD-10). The counts stay as history.</summary>
public sealed record ClearCodFlagCommand(string Mobile, string StaffRef) : IRequest<bool>, IOrdersCommand;

public sealed class ClearCodFlagCommandHandler(IOrdersDbContext dbContext, TimeProvider clock) : IRequestHandler<ClearCodFlagCommand, bool>
{
    public async Task<bool> Handle(ClearCodFlagCommand request, CancellationToken cancellationToken)
    {
        var risk = await dbContext.CodRisks.FirstOrDefaultAsync(r => r.Mobile == request.Mobile, cancellationToken);
        if (risk is null)
        {
            return false;
        }

        risk.Clear(request.StaffRef, clock.GetUtcNow());
        // Orders still waiting no longer show the flag either.
        foreach (var order in await dbContext.Orders.Where(o => o.ContactMobile == request.Mobile && o.IsRefusalFlagged).ToListAsync(cancellationToken))
        {
            order.ClearRefusalFlag();
        }

        return true;
    }
}
