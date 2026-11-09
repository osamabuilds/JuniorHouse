using MediatR;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Orders.Application.Payments;
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

    /// <summary>First confirmation call is due this long after placing (COD-1). Default 60.</summary>
    public int CodFirstCallDelayMinutes { get; set; } = 60;

    public int CodRetryGapMinutes { get; set; } = 120;

    public int CodMaxAttempts { get; set; } = 2;

    public int CodFinalWaitHours { get; set; } = 24;

    public int WhatsAppConfirmAfterMinutes { get; set; } = 60;

    /// <summary>Refusals from one mobile before staff see a flag (COD-5). Default 2. Flags only, never a block.</summary>
    public int RefusalFlagThreshold { get; set; } = 2;

    /// <summary>A cash on delivery total at or above this is flagged high value for staff (COD-6); 0 turns the flag off.</summary>
    public decimal HighValueFlagAmount { get; set; }

    /// <summary>How long a confirm or cancel link from the page or email works. Default 7 days.</summary>
    public int CodLinkDays { get; set; } = 7;

    /// <summary>The secret that signs confirm and view links (at least 32 characters). A development default is used only in Development.</summary>
    public string LinkSigningKey { get; set; } = string.Empty;

    /// <summary>How long an unpaid online order holds its stock before it expires (PAY-11, Q-RSV-2). Default 30.</summary>
    public int PaymentWindowMinutes { get; set; } = 30;

    /// <summary>Gateway sessions one order may open before it is cancelled as payment failed. Default 3.</summary>
    public int PaymentMaxAttempts { get; set; } = 3;

    /// <summary>One gateway call may take this long before it counts as a failure (SEC-21). Default 10.</summary>
    public int GatewayTimeoutSeconds { get; set; } = 10;

    /// <summary>Reconciliation looks at attempts still unanswered after this many minutes (Q-PAY-3). Default 10.</summary>
    public int ReconcileAfterMinutes { get; set; } = 10;

    /// <summary>...and stops after this many hours. Default 24.</summary>
    public int ReconcileMaxAgeHours { get; set; } = 24;

    /// <summary>The circuit opens after this many failures inside <see cref="CircuitWindowSeconds"/> (Q-PAY-5). Default 5.</summary>
    public int CircuitFailureThreshold { get; set; } = 5;

    public int CircuitWindowSeconds { get; set; } = 60;

    /// <summary>Seconds the circuit stays open before one trial call. Default 30.</summary>
    public int CircuitOpenSeconds { get; set; } = 30;

    /// <summary>The payment gateway to use: empty for none (Cash on Delivery only) or <c>fake</c> (Development and Testing only, PAY-15).</summary>
    public string GatewayProvider { get; set; } = string.Empty;

    /// <summary>At most this many rejected (forged or stale) callbacks are stored a minute across the host; the rest are only logged. Default 60.</summary>
    public int RejectedWebhooksStoredPerMinute { get; set; } = 60;

    /// <summary>Rejected-callback records older than this many days are deleted. Default 30.</summary>
    public int RejectedWebhookRetentionDays { get; set; } = 30;

    /// <summary>The secret the fake gateway signs its callbacks with (at least 16 characters). Only read by the fake.</summary>
    public string FakeGatewayWebhookSecret { get; set; } = string.Empty;

    public CodPolicy CodPolicy => new(
        TimeSpan.FromMinutes(CodFirstCallDelayMinutes), TimeSpan.FromMinutes(CodRetryGapMinutes), CodMaxAttempts, TimeSpan.FromHours(CodFinalWaitHours),
        TimeSpan.FromMinutes(WhatsAppConfirmAfterMinutes));

    public static bool IsValid(OrdersOptions options) =>
        options.EnabledOnlineMethods.All(c => c is "EASY" or "JAZZ" or "CARD")
        && options.CodFirstCallDelayMinutes is >= 1 and <= 1440 && options.CodRetryGapMinutes is >= 1 and <= 1440
        && options.CodMaxAttempts is >= 1 and <= 5 && options.CodFinalWaitHours is >= 1 and <= 168
        && options.WhatsAppConfirmAfterMinutes is >= 1 and <= 1440 && options.RefusalFlagThreshold is >= 1 and <= 20
        && options.HighValueFlagAmount >= 0 && options.CodLinkDays is >= 1 and <= 30
        && options.PaymentWindowMinutes is >= 5 and <= 1440 && options.PaymentMaxAttempts is >= 1 and <= 10 && options.GatewayTimeoutSeconds is >= 1 and <= 60
        && options.ReconcileAfterMinutes is >= 1 and <= 120 && options.ReconcileMaxAgeHours is >= 1 and <= 168
        && options.CircuitFailureThreshold is >= 1 and <= 100 && options.CircuitWindowSeconds is >= 1 and <= 3600 && options.CircuitOpenSeconds is >= 1 and <= 3600
        && options.RejectedWebhooksStoredPerMinute is >= 1 and <= 100000 && options.RejectedWebhookRetentionDays is >= 1 and <= 3650
        && options.GatewayProvider is "" or "none" or "fake"
        && (options.EnabledOnlineMethods.Length == 0 || options.GatewayProvider is "fake");
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
    IReadOnlyList<CheckoutAddressDto> SavedAddresses,
    bool OnlinePaymentsPaused = false);

public sealed record GetCheckoutSummaryQuery(int? CityId, string? MethodCode) : IRequest<CheckoutSummaryDto>;

/// <summary>The methods a shopper may choose now.</summary>
public static class PaymentMethodCatalog
{
    /// <summary>The methods on offer. <paramref name="gatewayAvailable"/> is false while no gateway works (none switched on, or its circuit is open): then only Cash on Delivery is offered (PAY-7, PAY-14).</summary>
    public static IReadOnlyList<PaymentMethodDto> Available(OrdersOptions options, bool gatewayAvailable = true) =>
    [
        new("COD", PaymentMethods.CashOnDelivery, "Cash on Delivery", false),
        .. new[]
        {
            new PaymentMethodDto("EASY", PaymentMethods.Easypaisa, "Easypaisa", true),
            new PaymentMethodDto("JAZZ", PaymentMethods.JazzCash, "JazzCash", true),
            new PaymentMethodDto("CARD", PaymentMethods.Card, "Card", true),
        }.Where(m => gatewayAvailable && options.EnabledOnlineMethods.Contains(m.Code)),
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

public sealed class GetCheckoutSummaryQueryHandler(ICheckoutPricing pricing, ICurrentCustomerAccessor customers, ICustomerAddressQueries addresses, IOptions<OrdersOptions> options, IGatewayAvailability gateway)
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
            PaymentMethodCatalog.Available(options.Value, gateway.IsAvailable), saved, gateway.IsPaused);
    }
}
