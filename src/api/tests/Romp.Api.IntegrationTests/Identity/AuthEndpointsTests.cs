using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Identity.Application.StaffAccounts;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Sign-in, refresh, sign-out and the session cookies through the real HTTP API against a real PostgreSQL (sessions task 12). The host
/// runs with real authentication switched on, so the development handler is not what lets requests in.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(ProductionApiFixture api)
{
    private const string Own = "Correct-Horse-Battery-Staple-42";
    private const string CsrfHeader = "X-Romp-Csrf";

    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private static readonly short[] CatalogueRole = [2];

    private WebApplicationFactory<Program> RealAuth(bool insecureCookies = true) => api.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Identity:UseRealAuthentication", "true");
        builder.UseSetting("Identity:AllowInsecureCookies", insecureCookies ? "true" : "false");
    });

    private static HttpClient Browser(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private async Task<(string Name, string TemporaryPassword, long UserId)> NewAccountAsync()
    {
        using var scope = api.Services.CreateScope();
        var name = $"auth.{Guid.NewGuid():N}"[..22];
        var created = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateStaffCommand(name, "Http Person", CatalogueRole));
        return (name, created.TemporaryPassword.Reveal(), created.UserId);
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string name, string password, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { userName = name, password }, options: Json),
        };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static string[] SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

    private static string CookieValue(HttpResponseMessage response, string name) =>
        SetCookies(response).Single(c => c.StartsWith(name + "=", StringComparison.Ordinal)).Split(';')[0][(name.Length + 1)..];

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-2")]
    [Trait("Spec", "AC-11")]
    public async Task FullFlow_SetsRotatesAndClearsCookies()
    {
        using var factory = RealAuth();
        using var browser = Browser(factory);
        var (name, temporary, _) = await NewAccountAsync();

        // The same answer for an unknown name and a wrong password (AC-7).
        var unknown = await SignInAsync(browser, "nobody.here.at.all", "whatever-it-is-123");
        var wrong = await SignInAsync(browser, name, "definitely-not-the-password");
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal((await BodyAsync(unknown)).GetProperty("title").GetString(), (await BodyAsync(wrong)).GetProperty("title").GetString()); // the trace id differs, nothing else
        Assert.Empty(SetCookies(wrong));

        // A new account holds a temporary password, so it gets a limited session (no MFA step yet).
        var signedIn = await SignInAsync(browser, name, temporary);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var body = await BodyAsync(signedIn);
        Assert.Equal("limited", body.GetProperty("status").GetString());
        Assert.Equal("ChangePassword", body.GetProperty("scope").GetString());
        Assert.Equal("no-store", signedIn.Headers.CacheControl?.ToString());
        var accessCookie = SetCookies(signedIn).Single(c => c.StartsWith("romp-acs=", StringComparison.Ordinal));
        var refreshCookie = SetCookies(signedIn).Single(c => c.StartsWith("romp-rfs=", StringComparison.Ordinal));
        Assert.All(new[] { accessCookie, refreshCookie }, c =>
        {
            Assert.Contains("httponly", c, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", c, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("path=/api/auth/refresh", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CookieValue(signedIn, "romp-acs"), await signedIn.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // /me says who the caller is and hands out the anti-forgery value.
        var me = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("no-store", me.Headers.CacheControl?.ToString());
        var meBody = await BodyAsync(me);
        Assert.Equal(name, meBody.GetProperty("userName").GetString());
        Assert.Equal("ChangePassword", meBody.GetProperty("scope").GetString());
        Assert.False(string.IsNullOrEmpty(meBody.GetProperty("csrfToken").GetString()));

        // Refresh rotates both values; the browser keeps working on the new pair.
        var oldRefresh = CookieValue(signedIn, "romp-rfs");
        var refreshed = await browser.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.NoContent, refreshed.StatusCode);
        Assert.NotEqual(oldRefresh, CookieValue(refreshed, "romp-rfs"));
        Assert.NotEqual(CookieValue(signedIn, "romp-acs"), CookieValue(refreshed, "romp-acs"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);

        // Sign-out ends the session and clears every cookie.
        var signedOut = await browser.PostAsync("/api/auth/sign-out", null);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Contains(SetCookies(signedOut), c => c.StartsWith("romp-acs=;", StringComparison.Ordinal) && c.Contains("1970", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);

        // Signing out again is harmless.
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/auth/sign-out", null)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task ARefreshValueUsedTwiceInARow_IsRefusedWithoutEndingTheSession()
    {
        using var factory = RealAuth();
        using var browser = Browser(factory);
        var (name, temporary, _) = await NewAccountAsync();
        var signedIn = await SignInAsync(browser, name, temporary);
        var stale = CookieValue(signedIn, "romp-rfs");
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/auth/refresh", null)).StatusCode);

        // A second request that still carries the previous value, inside the grace: told to retry, not signed out.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"romp-rfs={stale}");
        using var plain = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var late = await plain.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Empty(SetCookies(late));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Refresh_WithoutAUsableCookie_Returns401_AndClearsTheCookies()
    {
        using var factory = RealAuth();
        using var plain = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var none = await plain.PostAsync("/api/auth/refresh", null);
        using var junk = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        junk.Headers.Add("Cookie", "romp-rfs=not-a-real-value");
        var bad = await plain.SendAsync(junk);

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Contains(SetCookies(bad), c => c.StartsWith("romp-acs=;", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task SignOutEverywhere_RevokesAll()
    {
        using var factory = RealAuth();
        using var laptop = Browser(factory);
        using var phone = Browser(factory);
        var (name, temporary, _) = await NewAccountAsync();
        await SignInAsync(laptop, name, temporary);
        var csrf = (await BodyAsync(await laptop.GetAsync("/api/auth/me"))).GetProperty("csrfToken").GetString()!;

        // A temporary password works once, so the person chooses their own before signing in on a second device.
        using var change = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password") { Content = JsonContent.Create(new { currentPassword = temporary, newPassword = Own }, options: Json) };
        change.Headers.Add(CsrfHeader, csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await laptop.SendAsync(change)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(phone, name, Own)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/auth/me")).StatusCode);

        // Without the anti-forgery value the request is refused and nothing ends (AC-10).
        Assert.Equal(HttpStatusCode.Forbidden, (await laptop.PostAsync("/api/auth/sign-out-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/auth/me")).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out-all");
        request.Headers.Add(CsrfHeader, csrf);
        var response = await laptop.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task UnsafeRequests_FromASession_NeedTheAntiForgeryHeader()
    {
        using var factory = RealAuth();
        using var browser = Browser(factory);
        var (name, temporary, _) = await NewAccountAsync();
        await SignInAsync(browser, name, temporary);
        var csrf = (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("csrfToken").GetString()!;
        var change = new { currentPassword = temporary, newPassword = Own };

        var without = await browser.PostAsJsonAsync("/api/auth/password", change, Json);
        using var wrong = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password") { Content = JsonContent.Create(change, options: Json) };
        wrong.Headers.Add(CsrfHeader, "wrong");
        var withWrong = await browser.SendAsync(wrong);
        using var right = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password") { Content = JsonContent.Create(change, options: Json) };
        right.Headers.Add(CsrfHeader, csrf);
        var withRight = await browser.SendAsync(right);

        Assert.Equal(HttpStatusCode.Forbidden, without.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withWrong.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withRight.StatusCode);

        // The password change moved the account on: the same session now only owes MFA enrolment.
        Assert.Equal("EnrolMfa", (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("scope").GetString());
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task SignIn_FromAnotherOrigin_IsRefused()
    {
        using var factory = RealAuth();
        using var browser = Browser(factory);
        var (name, temporary, _) = await NewAccountAsync();

        var foreign = await SignInAsync(browser, name, temporary, origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Empty(SetCookies(foreign));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task RepeatedFailures_AreThrottled_WithRetryAfterOnly()
    {
        using var factory = RealAuth();
        using var browser = Browser(factory);
        var (name, temporary, _) = await NewAccountAsync();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(browser, name, "wrong-password-" + i)).StatusCode);
        }

        var blocked = await SignInAsync(browser, name, temporary); // even the right password waits
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero);
        Assert.Empty(SetCookies(blocked));
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Cookies_AreSecureByDefault()
    {
        using var factory = RealAuth(insecureCookies: false);
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var (name, temporary, _) = await NewAccountAsync();

        var signedIn = await SignInAsync(browser, name, temporary);

        var cookies = SetCookies(signedIn);
        Assert.Equal(2, cookies.Length);
        Assert.All(cookies, c =>
        {
            Assert.StartsWith("__Secure-romp-", c, StringComparison.Ordinal);
            Assert.Contains("secure", c, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", c, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", c, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task MfaVerify_IsNotAvailableUntilMfaIsBuilt()
    {
        using var factory = RealAuth();
        using var browser = Browser(factory);

        var response = await browser.PostAsync("/api/auth/mfa/verify", null);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task Me_WithoutASession_Returns401()
    {
        using var factory = RealAuth();
        using var plain = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await plain.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(SetCookies(response));
    }
}
