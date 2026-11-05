using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Orders;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>Signed confirm links for cash on delivery orders (ICodLinks). The key comes from <see cref="OrdersLinkSigner"/>.</summary>
internal sealed class CodLinks(IOptions<OrdersOptions> options, OrdersLinkSigner signer) : ICodLinks
{
    private const string Purpose = "cod-respond";

    public string Create(string orderNumber, DateTimeOffset now) => signer.Links.Create(Purpose, orderNumber, now.AddDays(options.Value.CodLinkDays));

    public string? TryRead(string? token, DateTimeOffset now) => signer.Links.TryValidate(Purpose, token, now, out var subject) ? subject : null;
}
