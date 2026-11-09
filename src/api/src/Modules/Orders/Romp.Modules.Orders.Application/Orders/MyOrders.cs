using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.OrderQueue;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Orders;

/// <summary>One row of the signed-in customer's own order list. Nothing about anyone else, and no contact details.</summary>
public sealed record MyOrderListItemDto(string OrderNumber, DateTimeOffset PlacedAt, string StatusName, string MethodName, decimal Total, int ItemCount, string PaymentStatusName);

public sealed record MyOrderLineDto(string Name, string Colour, string Size, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record MyOrderStepDto(string StatusName, DateTimeOffset At);

public sealed record MyOrderDetailDto(
    string OrderNumber,
    DateTimeOffset PlacedAt,
    string StatusName,
    string MethodName,
    string PaymentStatusName,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    decimal Total,
    string RecipientName,
    string AddressLine1,
    string? AddressLine2,
    string CityName,
    string ProvinceName,
    IReadOnlyList<MyOrderLineDto> Lines,
    IReadOnlyList<MyOrderStepDto> Timeline);

/// <summary>
/// The signed-in customer's own orders, newest first, paged (ORD-14). The customer is the one on the session, never a value in the request, and
/// is part of the same statement that reads the rows, so another customer's order cannot be returned (ORD-13, SEC-05). Two statements for a page
/// however many orders there are: a count and the page, with the line count and payment status as subqueries (no N+1).
/// </summary>
public sealed record ListMyOrdersQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<MyOrderListItemDto>>;

public sealed class ListMyOrdersQueryHandler(IOrdersDbContext dbContext, ICurrentCustomerAccessor customers) : IRequestHandler<ListMyOrdersQuery, PagedResult<MyOrderListItemDto>>
{
    public async Task<PagedResult<MyOrderListItemDto>> Handle(ListMyOrdersQuery request, CancellationToken cancellationToken)
    {
        var request2 = new PageRequest(request.Page, request.PageSize);
        if (await customers.ResolveAsync(cancellationToken) is not { } customerId)
        {
            return new PagedResult<MyOrderListItemDto>([], request2.Normalized().Page, request2.Normalized().PageSize, 0);
        }

        var page = await dbContext.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.Id)
            .Select(o => new
            {
                o.OrderNumber,
                o.PlacedAt,
                o.StatusId,
                o.PaymentMethodId,
                o.Total,
                ItemCount = o.Lines.Sum(l => l.Quantity),
                PaymentStatusId = dbContext.Payments.Where(p => p.OrderId == o.Id).Select(p => (short?)p.StatusId).FirstOrDefault(),
            })
            .ToPagedResultAsync(request2, cancellationToken);

        var items = page.Items.Select(o => new MyOrderListItemDto(
            o.OrderNumber, o.PlacedAt, OrderLabels.Status(o.StatusId), OrderLabels.Method(o.PaymentMethodId), o.Total, o.ItemCount, PaymentLabel(o.PaymentStatusId))).ToList();
        return new PagedResult<MyOrderListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>What a customer is told about the payment. An order under review says so without saying why.</summary>
    internal static string PaymentLabel(short? status) => status switch
    {
        PaymentStatuses.Initiated => "Not started",
        PaymentStatuses.Pending => "Pending",
        PaymentStatuses.Paid => "Paid",
        PaymentStatuses.Failed => "Failed",
        PaymentStatuses.Expired => "Expired",
        PaymentStatuses.InReview => "Being checked",
        _ => "Unknown",
    };
}

/// <summary>One of the customer's own orders. Anyone else's order, or none at all, is the same answer: not found (ORD-13).</summary>
public sealed record GetMyOrderQuery(string? OrderNumber) : IRequest<MyOrderDetailDto?>;

public sealed class GetMyOrderQueryHandler(IOrdersDbContext dbContext, ICurrentCustomerAccessor customers) : IRequestHandler<GetMyOrderQuery, MyOrderDetailDto?>
{
    public async Task<MyOrderDetailDto?> Handle(GetMyOrderQuery request, CancellationToken cancellationToken)
    {
        var number = OrderNumbers.Normalise(request.OrderNumber);
        if (number is null || await customers.ResolveAsync(cancellationToken) is not { } customerId)
        {
            return null;
        }

        var order = await dbContext.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.Lines)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.OrderNumber == number && o.CustomerId == customerId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var paymentStatus = await dbContext.Payments.AsNoTracking().Where(p => p.OrderId == order.Id).Select(p => (short?)p.StatusId).FirstOrDefaultAsync(cancellationToken);
        return new MyOrderDetailDto(
            order.OrderNumber, order.PlacedAt, OrderLabels.Status(order.StatusId), OrderLabels.Method(order.PaymentMethodId), ListMyOrdersQueryHandler.PaymentLabel(paymentStatus),
            order.Subtotal, order.Discount, order.Shipping, order.Total, order.RecipientName, order.AddressLine1, order.AddressLine2, order.CityName, order.ProvinceName,
            [.. order.Lines.OrderBy(l => l.Id).Select(l => new MyOrderLineDto(l.Name, l.Colour, l.Size, l.UnitPrice, l.Quantity, l.LineTotal))],
            [.. order.History.OrderBy(h => h.Id).Select(h => new MyOrderStepDto(OrderLabels.Status(h.ToStatusId), h.At))]);
    }
}
