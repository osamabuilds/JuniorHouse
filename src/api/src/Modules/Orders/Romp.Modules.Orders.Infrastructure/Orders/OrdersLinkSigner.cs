using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Security;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// The one place the Orders signing secret (<c>Orders:LinkSigningKey</c>) is read. Outside Development a missing or short key stops the
/// first use loudly rather than signing with something guessable; in Development a fixed local value is used so the demo works.
/// </summary>
internal sealed class OrdersLinkSigner(IOptions<OrdersOptions> options, IHostEnvironment environment)
{
    private const string DevelopmentKey = "development-only-orders-link-signing-key-do-not-use";

    private readonly Lazy<byte[]> _key = new(() =>
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

        return Encoding.UTF8.GetBytes(key);
    });

    private SignedLink? _links;

    public byte[] Key => _key.Value;

    public SignedLink Links => _links ??= new SignedLink(Key);
}
