using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Infrastructure.Orders;

namespace Romp.Modules.Orders.Infrastructure.Payments;

/// <summary>
/// The signed token that names one order's payment (<see cref="IPaymentLinks"/>). It works for the payment window plus a day, so a shopper can
/// come back to see the result. A different purpose from the cash on delivery links, so the two can never be swapped for each other.
/// </summary>
internal sealed class PaymentLinks(IOptions<OrdersOptions> options, IOptions<SiteOptions> site, OrdersLinkSigner signer) : IPaymentLinks
{
    private const string Purpose = "pay-order";

    public string Create(string orderNumber, DateTimeOffset now) =>
        signer.Links.Create(Purpose, orderNumber, now.AddMinutes(options.Value.PaymentWindowMinutes).AddDays(1));

    public string? TryRead(string? token, DateTimeOffset now) => signer.Links.TryValidate(Purpose, token, now, out var subject) ? subject : null;

    public string ReturnUrl(string token)
    {
        var baseUrl = site.Value.PublicBaseUrl?.TrimEnd('/') ?? string.Empty;
        return $"{baseUrl}/checkout/payment-return?token={Uri.EscapeDataString(token)}";
    }
}
