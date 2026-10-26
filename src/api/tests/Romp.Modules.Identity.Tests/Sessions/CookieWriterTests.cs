using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Infrastructure.Sessions;

namespace Romp.Modules.Identity.Tests.Sessions;

public sealed class CookieWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static SessionCookieWriter Writer(bool insecure = false) =>
        new(Options.Create(new IdentityOptions { AllowInsecureCookies = insecure }));

    private static IssuedTokens Tokens() => new(SessionTokens.Generate(), SessionTokens.Generate());

    private static List<SetCookieHeaderValue> Cookies(HttpResponse response) =>
        SetCookieHeaderValue.ParseList(response.Headers.SetCookie).ToList();

    private static SetCookieHeaderValue Single(IEnumerable<SetCookieHeaderValue> cookies, string name) =>
        Assert.Single(cookies, c => c.Name == name);

    [Fact]
    [Trait("Spec", "AC-1")]
    public void SetsEveryRequiredFlag()
    {
        var response = new DefaultHttpContext().Response;
        var tokens = Tokens();

        Writer().WriteSession(response, tokens, Now.AddMinutes(15), Now.AddMinutes(30));

        var cookies = Cookies(response);
        Assert.Equal(2, cookies.Count);

        var access = Single(cookies, "__Secure-romp-acs");
        var refresh = Single(cookies, "__Secure-romp-rfs");
        Assert.Equal(tokens.Access.Reveal(), access.Value.Value);
        Assert.Equal(tokens.Refresh.Reveal(), refresh.Value.Value);
        Assert.Equal("/api", access.Path.Value);
        Assert.Equal("/api/auth/refresh", refresh.Path.Value); // the long-lived value travels only to the refresh endpoint
        Assert.Equal(Now.AddMinutes(15), access.Expires);
        Assert.Equal(Now.AddMinutes(30), refresh.Expires);
        Assert.All(cookies, c =>
        {
            Assert.True(c.HttpOnly);
            Assert.True(c.Secure);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, c.SameSite);
            Assert.Null(c.Domain.Value); // host-only: never shared with subdomains
        });
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void ThePendingCookie_IsScopedToTheMfaEndpoint_AndShortLived()
    {
        var response = new DefaultHttpContext().Response;
        var token = SessionTokens.Generate();

        Writer().WritePending(response, token, Now.AddMinutes(5));

        var pending = Single(Cookies(response), "__Secure-romp-pnd");
        Assert.Equal(token.Reveal(), pending.Value.Value);
        Assert.Equal("/api/auth/mfa", pending.Path.Value);
        Assert.Equal(Now.AddMinutes(5), pending.Expires);
        Assert.True(pending.HttpOnly);
        Assert.True(pending.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, pending.SameSite);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Clear_ExpiresAllThreeCookies_OnTheirOwnPaths()
    {
        var response = new DefaultHttpContext().Response;

        Writer().Clear(response);

        var cookies = Cookies(response);
        Assert.Equal(3, cookies.Count);
        Assert.Equal("/api", Single(cookies, "__Secure-romp-acs").Path.Value);
        Assert.Equal("/api/auth/refresh", Single(cookies, "__Secure-romp-rfs").Path.Value);
        Assert.Equal("/api/auth/mfa", Single(cookies, "__Secure-romp-pnd").Path.Value);
        Assert.All(cookies, c =>
        {
            Assert.Equal(string.Empty, c.Value.Value);
            Assert.True(c.Expires < DateTimeOffset.UtcNow.AddYears(-1)); // in the past, so the browser drops it
            Assert.True(c.HttpOnly);
            Assert.True(c.Secure);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, c.SameSite);
        });
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void DevelopmentOverHttp_DropsThePrefixAndTheSecureFlag_Only()
    {
        var response = new DefaultHttpContext().Response;

        Writer(insecure: true).WriteSession(response, Tokens(), Now.AddMinutes(15), Now.AddMinutes(30));

        var cookies = Cookies(response);
        var access = Single(cookies, "romp-acs");
        var refresh = Single(cookies, "romp-rfs");
        Assert.All(new[] { access, refresh }, c =>
        {
            Assert.False(c.Secure); // a browser refuses a Secure cookie over plain HTTP
            Assert.True(c.HttpOnly);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, c.SameSite);
        });
        Assert.DoesNotContain(cookies, c => c.Name.StartsWith("__Secure-", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void NoStore_IsSetOnAuthResponses()
    {
        var response = new DefaultHttpContext().Response;

        SessionCookieWriter.NoStore(response);

        Assert.Equal("no-store", response.Headers.CacheControl.ToString());
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void CookieNames_FollowTheInsecureSwitch()
    {
        Assert.Equal("__Secure-romp-acs", SessionCookieNames.For(allowInsecure: false).Access);
        Assert.Equal("romp-acs", SessionCookieNames.For(allowInsecure: true).Access);
    }
}

public sealed class SessionLifetimeOptionsTests
{
    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSessionLifetimes(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void WithoutConfiguration_TheSpecDefaultsApply()
    {
        using var provider = Provider([]);

        Assert.Equal(SessionLifetimes.Default, provider.GetRequiredService<SessionLifetimes>());
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void ConfiguredMinutes_AreUsed()
    {
        using var provider = Provider(new()
        {
            ["Identity:Sessions:AccessMinutes"] = "5",
            ["Identity:Sessions:IdleMinutes"] = "10",
            ["Identity:Sessions:AbsoluteMinutes"] = "60",
        });

        var lifetimes = provider.GetRequiredService<SessionLifetimes>();

        Assert.Equal(TimeSpan.FromMinutes(5), lifetimes.Access);
        Assert.Equal(TimeSpan.FromMinutes(10), lifetimes.Idle);
        Assert.Equal(TimeSpan.FromMinutes(60), lifetimes.Absolute);
    }

    [Theory]
    [Trait("Spec", "AC-2")]
    [InlineData("0", "30", "720")]    // an access cookie that lives zero minutes
    [InlineData("40", "30", "720")]   // access outlives the idle window
    [InlineData("15", "900", "720")]  // idle outlives the absolute limit
    [InlineData("15", "30", "100000")] // absurdly long absolute limit
    public void InvalidCombinations_AreRefused(string access, string idle, string absolute)
    {
        using var provider = Provider(new()
        {
            ["Identity:Sessions:AccessMinutes"] = access,
            ["Identity:Sessions:IdleMinutes"] = idle,
            ["Identity:Sessions:AbsoluteMinutes"] = absolute,
        });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<SessionLifetimes>());
    }
}
