using MediatR;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Application.Checkout;

/// <summary>Settings of the Orders module (<c>Orders:*</c>), validated at start.</summary>
public sealed class OrdersOptions
{
    public const string SectionName = "Orders";

    /// <summary>
    /// Online payment methods the shop has switched on, by code (<c>EASY</c>, <c>JAZZ</c>, <c>CARD</c>). Empty until the owner has a payment
    /// aggregator (Q-PAY-1): then only Cash on Delivery is offered. Cash on Delivery is always available (PAY-7).
    /// </summary>
    public string[] EnabledOnlineMethods { get; set; } = [];

    public static bool IsValid(OrdersOptions options) =>
        options.EnabledOnlineMethods.All(c => c is "EASY" or "JAZZ" or "CARD");
}

public sealed record PaymentMethodDto(string Code, short Id, string Name, bool IsOnline);

public sealed record CheckoutAddressDto(long Id, string RecipientName, string Phone, short ProvinceId, short CityId, string LineOne, string? LineTwo, string? Postcode, bool IsDefault);

/// <summary>What the checkout page shows: the priced basket (every figure the server's), the methods on offer and the signed-in customer's saved addresses (CHK-4, CHK-9).</summary>
public sealed record CheckoutSummaryDto(
    IReadOnlyList<CheckoutLine> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal? Shipping,
    decimal Total,
    bool CanOrder,
    IReadOnlyList<string> Problems,
    IReadOnlyList<PaymentMethodDto> PaymentMethods,
    IReadOnlyList<CheckoutAddressDto> SavedAddresses);

public sealed record GetCheckoutSummaryQuery(int? CityId, string? MethodCode) : IRequest<CheckoutSummaryDto>;

/// <summary>The methods a shopper may choose now.</summary>
public static class PaymentMethodCatalog
{
    public static IReadOnlyList<PaymentMethodDto> Available(OrdersOptions options) =>
    [
        new("COD", PaymentMethods.CashOnDelivery, "Cash on Delivery", false),
        .. new[]
        {
            new PaymentMethodDto("EASY", PaymentMethods.Easypaisa, "Easypaisa", true),
            new PaymentMethodDto("JAZZ", PaymentMethods.JazzCash, "JazzCash", true),
            new PaymentMethodDto("CARD", PaymentMethods.Card, "Card", true),
        }.Where(m => options.EnabledOnlineMethods.Contains(m.Code)),
    ];

    public static short? IdOf(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "COD" => PaymentMethods.CashOnDelivery,
        "EASY" => PaymentMethods.Easypaisa,
        "JAZZ" => PaymentMethods.JazzCash,
        "CARD" => PaymentMethods.Card,
        _ => null,
    };
}

public sealed class GetCheckoutSummaryQueryHandler(ICheckoutPricing pricing, ICurrentCustomerAccessor customers, ICustomerAddressQueries addresses, IOptions<OrdersOptions> options)
    : IRequestHandler<GetCheckoutSummaryQuery, CheckoutSummaryDto>
{
    public async Task<CheckoutSummaryDto> Handle(GetCheckoutSummaryQuery request, CancellationToken cancellationToken)
    {
        var isCod = !string.Equals(request.MethodCode, "EASY", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.MethodCode, "JAZZ", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.MethodCode, "CARD", StringComparison.OrdinalIgnoreCase);
        var priced = await pricing.PriceCurrentCartAsync(request.CityId, isCod, cancellationToken);

        // Saved addresses belong to the signed-in customer only; a guest gets none (SEC-05).
        IReadOnlyList<CheckoutAddressDto> saved = [];
        if (await customers.ResolveAsync(cancellationToken) is not null)
        {
            saved = [.. (await addresses.GetMyAddressesAsync(cancellationToken)).Select(a =>
                new CheckoutAddressDto(a.Id, a.RecipientName, a.Phone, a.ProvinceId, a.CityId, a.LineOne, a.LineTwo, a.Postcode, a.IsDefault))];
        }

        return new CheckoutSummaryDto(
            priced.Lines, priced.Subtotal, priced.Discount, priced.ShippingKnown ? priced.Shipping : null, priced.Total, priced.CanOrder, priced.Problems,
            PaymentMethodCatalog.Available(options.Value), saved);
    }
}
