using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Captcha;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Application.OrderLookup;

/// <summary>Makes and reads the signed "track my order" links (OLK-5). A link names one order, never expires (Q-OLK-2), and shows only masked data.</summary>
public interface ILookupLinks
{
    string Create(string orderNumber);

    /// <summary>The order number inside a valid token, or null for anything else.</summary>
    string? TryRead(string? token);
}

/// <summary>A keyed hash of a client address, so the attempts table can drive the throttle without holding addresses (SEC-25).</summary>
public interface ILookupAddressHasher
{
    /// <summary>Always <see cref="LookupAttempt.AddressHashLength"/> lowercase hex characters.</summary>
    string Hash(string? clientIp);
}

public sealed record TrackedLine(string Name, string Colour, string Size, int Quantity, decimal LineTotal);

/// <summary>
/// What a guest sees (OLK-1, OLK-6): the status, the items and the totals. The mobile is masked and only the city of the address is
/// given, never the street, the recipient or the full phone number.
/// </summary>
public sealed record TrackedOrderDto(
    string OrderNumber,
    string StatusCode,
    string StatusName,
    DateTimeOffset PlacedAt,
    string PaymentMethodCode,
    string MaskedMobile,
    string City,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    decimal Total,
    IReadOnlyList<TrackedLine> Lines,
    Romp.Modules.Shipping.Contracts.ShipmentTrackingView? Tracking = null);

public enum LookupStatus
{
    Found,

    /// <summary>One answer for a wrong number, a wrong mobile, both, or a number that is not an order number (OLK-2, SEC-07).</summary>
    Mismatch,
    CaptchaRequired,
    RateLimited,
}

public sealed record LookupResult(LookupStatus Status, TrackedOrderDto? Order = null)
{
    public static readonly LookupResult Mismatch = new(LookupStatus.Mismatch);
}

internal static class OrderStatusNames
{
    public static string NameOf(short id) => id switch
    {
        OrderStatuses.PendingConfirmation => "Waiting for your confirmation",
        OrderStatuses.Confirmed => "Confirmed",
        OrderStatuses.PaymentPending => "Waiting for payment",
        OrderStatuses.Paid => "Paid",
        OrderStatuses.Cancelled => "Cancelled",
        OrderStatuses.Dispatched => "On its way",
        OrderStatuses.Delivered => "Delivered",
        _ => "Processing",
    };
}

internal static class TrackedOrders
{
    /// <summary>One query for an order's lines (id 0 finds none, so a miss costs the same as a hit).</summary>
    public static Task<List<OrderLine>> LinesAsync(IOrdersDbContext dbContext, long orderId, CancellationToken cancellationToken) =>
        dbContext.OrderLines.AsNoTracking().Where(l => l.OrderId == orderId).OrderBy(l => l.Id).ToListAsync(cancellationToken);

    public static TrackedOrderDto Build(Order order, IReadOnlyList<OrderLine> lines, Romp.Modules.Shipping.Contracts.ShipmentTrackingView? tracking = null) => new TrackedOrderDto(
            order.OrderNumber,
            OrderStatuses.CodeOf(order.StatusId),
            OrderStatusNames.NameOf(order.StatusId),
            order.PlacedAt,
            PaymentMethods.CodeOf(order.PaymentMethodId),
            ContactMasking.MaskMobile(order.ContactMobile),
            order.CityName,
            order.Subtotal,
            order.Discount,
            order.Shipping,
            order.Total,
            lines.Select(l => new TrackedLine(l.Name, l.Colour, l.Size, l.Quantity, l.LineTotal)).ToList(),
            tracking);
}

/// <summary>
/// Guest order look-up by order number and mobile (FR-37, SEC-06, SEC-07). Every kind of mismatch gets the same answer, and the work done
/// is the same whether or not the order exists. Failures raise the CAPTCHA, and an address is cut off after too many attempts. An attempt
/// is recorded only as a hashed address plus pass or fail, never the number or mobile that was typed.
/// </summary>
public sealed record LookupOrderQuery(string? OrderNumber, string? Mobile, string? CaptchaToken, string? ClientIp) : IRequest<LookupResult>, IOrdersCommand;

public sealed class LookupOrderQueryHandler(
    IOrdersDbContext dbContext, ICaptchaVerifier captcha, ILookupAddressHasher hasher, TimeProvider clock, Romp.Modules.Shipping.Contracts.IShipmentTrackingQueries tracking)
    : IRequestHandler<LookupOrderQuery, LookupResult>
{
    // Compared when there is no such order, so the comparison always runs on two real values.
    private const string AbsentMobile = "+920000000000";

    public async Task<LookupResult> Handle(LookupOrderQuery request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var address = hasher.Hash(request.ClientIp);

        // Newest first, at most one more than the cut-off: enough to count attempts and the failures since the last success.
        var recent = await dbContext.LookupAttempts.AsNoTracking()
            .Where(a => a.AddressHash == address && a.AttemptedAt > now - LookupThrottle.Window)
            .OrderByDescending(a => a.AttemptedAt)
            .Select(a => a.Failed)
            .Take(LookupThrottle.MaxAttemptsPerWindow)
            .ToListAsync(cancellationToken);
        var failures = recent.TakeWhile(failed => failed).Count();
        var decision = LookupThrottle.Evaluate(failures, recent.Count);

        if (decision.RateLimited)
        {
            return new LookupResult(LookupStatus.RateLimited);
        }

        if (decision.CaptchaRequired && !await captcha.VerifyAsync(request.CaptchaToken, request.ClientIp, cancellationToken))
        {
            dbContext.LookupAttempts.Add(LookupAttempt.Record(address, failed: true, now));
            return new LookupResult(LookupStatus.CaptchaRequired);
        }

        var number = OrderNumbers.Normalise(request.OrderNumber);
        var typedMobile = PhoneNumber.TryNormalise(request.Mobile, out var normalised) ? normalised : null;

        var order = number is null
            ? null
            : await dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNumber == number, cancellationToken);

        var onOrder = Digest(PhoneNumber.TryNormalise(order?.ContactMobile, out var stored) ? stored : AbsentMobile);
        var typed = Digest(typedMobile ?? "not-a-mobile");
        var mobileMatches = CryptographicOperations.FixedTimeEquals(onOrder, typed);

        // The lines query runs for a miss too (with no order it finds nothing), so a hit and a miss cost the same.
        var lines = await TrackedOrders.LinesAsync(dbContext, order?.Id ?? 0, cancellationToken);

        if (order is null || typedMobile is null || !mobileMatches)
        {
            dbContext.LookupAttempts.Add(LookupAttempt.Record(address, failed: true, now));
            return LookupResult.Mismatch;
        }

        dbContext.LookupAttempts.Add(LookupAttempt.Record(address, failed: false, now));
        return new LookupResult(LookupStatus.Found, TrackedOrders.Build(order, lines, await tracking.FindByOrderAsync(order.Id, cancellationToken)));
    }

    private static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}

/// <summary>The signed link from the order email (OLK-5): opens the same masked status page with no further input.</summary>
public sealed record ViewOrderByLinkQuery(string? Token) : IRequest<TrackedOrderDto?>;

public sealed class ViewOrderByLinkQueryHandler(IOrdersDbContext dbContext, ILookupLinks links, Romp.Modules.Shipping.Contracts.IShipmentTrackingQueries tracking) : IRequestHandler<ViewOrderByLinkQuery, TrackedOrderDto?>
{
    public async Task<TrackedOrderDto?> Handle(ViewOrderByLinkQuery request, CancellationToken cancellationToken)
    {
        var number = links.TryRead(request.Token);
        var order = number is null ? null : await dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNumber == number, cancellationToken);
        return order is null ? null : TrackedOrders.Build(order, await TrackedOrders.LinesAsync(dbContext, order.Id, cancellationToken), await tracking.FindByOrderAsync(order.Id, cancellationToken));
    }
}
