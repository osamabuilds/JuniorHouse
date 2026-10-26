using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Romp.BuildingBlocks.Web;

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Believes the forwarded address and scheme only when the connection comes from a proxy listed in <c>Network:TrustedProxies</c>
    /// (sessions AC-15). <c>Network:ClientIpHeader</c> names the header that carries the visitor address: <c>X-Forwarded-For</c> by
    /// default, or <c>CF-Connecting-IP</c> when Cloudflare reaches the API through the reverse proxy. Nothing is trusted by default.
    /// </summary>
    public static IServiceCollection AddRompForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<NetworkOptions>>((options, network) =>
        {
            // With both lists empty the framework trusts EVERY peer, so with nothing configured forwarding is switched off instead.
            options.ForwardedHeaders = network.Value.TrustedProxies.Length == 0
                ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardedForHeaderName = network.Value.ClientIpHeader;
            options.KnownProxies.Clear();   // the framework default trusts loopback; trust only what is configured
            options.KnownIPNetworks.Clear();
            foreach (var entry in network.Value.TrustedProxies)
            {
                if (IPAddress.TryParse(entry, out var proxy))
                {
                    options.KnownProxies.Add(proxy);
                }
                else if (System.Net.IPNetwork.TryParse(entry, out var range))
                {
                    options.KnownIPNetworks.Add(range);
                }
            }
        });
        return services;
    }

    /// <summary>Must run before anything that reads the client address: the rate limiter, authentication and sign-in.</summary>
    public static IApplicationBuilder UseRompForwardedHeaders(this IApplicationBuilder app) => app.UseForwardedHeaders();

    /// <summary>
    /// The visitor address as text that fits the stored columns: an IPv4 address seen over a dual-stack socket reads as IPv4, and
    /// "unknown" when the connection has none.
    /// </summary>
    public static string ClientIp(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}
