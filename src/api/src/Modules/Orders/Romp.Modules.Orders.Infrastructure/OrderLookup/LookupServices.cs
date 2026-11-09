using System.Security.Cryptography;
using System.Text;
using Romp.Modules.Orders.Application.OrderLookup;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Orders;

namespace Romp.Modules.Orders.Infrastructure.OrderLookup;

/// <summary>Signed track-my-order links. No expiry (Q-OLK-2): the page behind them shows only status and masked data.</summary>
internal sealed class LookupLinks(OrdersLinkSigner signer, TimeProvider clock) : ILookupLinks
{
    private const string Purpose = "order-track";

    public string Create(string orderNumber) => signer.Links.Create(Purpose, orderNumber);

    public string? TryRead(string? token) => signer.Links.TryValidate(Purpose, token, clock.GetUtcNow(), out var subject) ? subject : null;
}

/// <summary>HMAC-SHA256 of the client address under the Orders secret, as lowercase hex (64 characters). Cannot be undone by trying every IPv4 address.</summary>
internal sealed class LookupAddressHasher(OrdersLinkSigner signer) : ILookupAddressHasher
{
    public string Hash(string? clientIp)
    {
        var mac = HMACSHA256.HashData(signer.Key, Encoding.UTF8.GetBytes($"lookup-address|{(clientIp ?? string.Empty).Trim()}"));
        var hex = Convert.ToHexStringLower(mac);
        return hex.Length == LookupAttempt.AddressHashLength ? hex : throw new InvalidOperationException("Unexpected hash length.");
    }
}
