using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Romp.BuildingBlocks.Web;

/// <summary><c>Site:*</c> - the public origin of the site. Code builds absolute URLs from it, never from the Host header.</summary>
public sealed class SiteOptions
{
    public const string Section = "Site";

    public string? PublicBaseUrl { get; set; }
}

/// <summary><c>Network:*</c> - the proxies whose forwarded headers are believed (sessions AC-15).</summary>
public sealed class NetworkOptions
{
    public const string Section = "Network";

    public string[] TrustedProxies { get; set; } = [];
}

/// <summary><c>Identity:*</c> - host-level switches for authentication.</summary>
public sealed class IdentityOptions
{
    public const string Section = "Identity";

    /// <summary>Unset means "the environment decides": real outside Development/Testing, the development handler inside.</summary>
    public bool? UseRealAuthentication { get; set; }

    /// <summary>
    /// Sends session cookies without the <c>Secure</c> flag and the <c>__Secure-</c> name prefix, so sign-in works over plain HTTP on
    /// a developer machine. Refused at start-up outside Development and Testing (sessions AC-1, AC-14).
    /// </summary>
    public bool AllowInsecureCookies { get; set; }

    public static bool IsRealAuthentication(IdentityOptions options, IHostEnvironment environment) =>
        !RompAuthorizationExtensions.IsDevelopmentLike(environment) || options.UseRealAuthentication == true;
}

public sealed class SiteOptionsValidator(IHostEnvironment environment) : IValidateOptions<SiteOptions>
{
    public ValidateOptionsResult Validate(string? name, SiteOptions options)
    {
        var development = RompAuthorizationExtensions.IsDevelopmentLike(environment);
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            return development
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("Site:PublicBaseUrl is required outside Development and Testing.");
        }

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return ValidateOptionsResult.Fail("Site:PublicBaseUrl must be an absolute http(s) URL.");
        }

        if (uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return ValidateOptionsResult.Fail("Site:PublicBaseUrl must be a bare origin: no credentials, path, query or fragment.");
        }

        if (!development && uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("Site:PublicBaseUrl must use https outside Development and Testing.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class NetworkOptionsValidator : IValidateOptions<NetworkOptions>
{
    private const int MinIPv4Prefix = 8;
    private const int MinIPv6Prefix = 16;

    public ValidateOptionsResult Validate(string? name, NetworkOptions options)
    {
        var errors = new List<string>();
        foreach (var entry in options.TrustedProxies)
        {
            if (!TryParse(entry, out var address, out var prefix))
            {
                errors.Add("Network:TrustedProxies entries must be an IP address or a CIDR network.");
                continue;
            }

            var minimum = address.AddressFamily == AddressFamily.InterNetwork ? MinIPv4Prefix : MinIPv6Prefix;
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || prefix < minimum)
            {
                errors.Add("Network:TrustedProxies must not trust every address; list the proxy's address or network.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors.Distinct());
    }

    private static bool TryParse(string entry, out IPAddress address, out int prefix)
    {
        if (IPAddress.TryParse(entry, out var single))
        {
            address = single;
            prefix = single.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            return true;
        }

        if (IPNetwork.TryParse(entry, out var network))
        {
            address = network.BaseAddress;
            prefix = network.PrefixLength;
            return true;
        }

        address = IPAddress.None;
        prefix = 0;
        return false;
    }
}

public sealed class IdentityOptionsValidator(IHostEnvironment environment) : IValidateOptions<IdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityOptions options)
    {
        if (RompAuthorizationExtensions.IsDevelopmentLike(environment))
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (options.UseRealAuthentication == false)
        {
            errors.Add("Identity:UseRealAuthentication cannot be false outside Development and Testing.");
        }

        if (options.AllowInsecureCookies)
        {
            errors.Add("Identity:AllowInsecureCookies cannot be true outside Development and Testing.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public static class HostOptionsExtensions
{
    /// <summary>Binds Site, Network and Identity and validates them when the host starts, so a bad deploy fails before serving.</summary>
    public static IServiceCollection AddRompHostOptions(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton(environment);
        services.AddOptions<SiteOptions>().Bind(configuration.GetSection(SiteOptions.Section)).ValidateOnStart();
        services.AddOptions<NetworkOptions>().Bind(configuration.GetSection(NetworkOptions.Section)).ValidateOnStart();
        services.AddOptions<IdentityOptions>().Bind(configuration.GetSection(IdentityOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SiteOptions>, SiteOptionsValidator>();
        services.AddSingleton<IValidateOptions<NetworkOptions>, NetworkOptionsValidator>();
        services.AddSingleton<IValidateOptions<IdentityOptions>, IdentityOptionsValidator>();
        return services;
    }
}
