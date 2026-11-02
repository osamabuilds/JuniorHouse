using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Security;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Orders;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// Signed confirm links for cash on delivery orders (ICodLinks). The key comes from <c>Orders:LinkSigningKey</c>, a secret. Outside
/// Development a missing or short key stops the first use loudly rather than signing with something guessable; in Development a fixed
/// local value is used so the demo works.
/// </summary>
internal sealed class CodLinks(IOptions<OrdersOptions> options, IHostEnvironment environment) : ICodLinks
{
    private const string Purpose = "cod-respond";
    private const string DevelopmentKey = "development-only-orders-link-signing-key-do-not-use";

    private readonly Lazy<SignedLink> _signer = new(() =>
    {
        var key = options.Value.LinkSigningKey;
        if (key.Length < SignedLink.MinKeyBytes)
        {
            if (!RompAuthorizationExtensions.IsDevelopmentLike(environment))
            {
                throw new InvalidOperationException("Orders:LinkSigningKey must be set to a secret of at least 32 characters.");
            }

            key = DevelopmentKey;
        }

        return new SignedLink(Encoding.UTF8.GetBytes(key));
    });

    public string Create(string orderNumber, DateTimeOffset now) => _signer.Value.Create(Purpose, orderNumber, now.AddDays(options.Value.CodLinkDays));

    public string? TryRead(string? token, DateTimeOffset now) => _signer.Value.TryValidate(Purpose, token, now, out var subject) ? subject : null;
}
