using Romp.BuildingBlocks.Web.Captcha;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Customers.Infrastructure.Accounts;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>
/// Customer sign-in through the real HTTP API against a real PostgreSQL (customers/accounts S7, S8): register, sign in, cookies, the
/// anti-forgery header, refresh, sign-out, email verification and password reset. The host runs with real customer sign-in switched on,
/// so the development stand-in is not what lets requests in. Every test uses its own client address, because the throttle counts per address.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class CustomerAuthEndpointsTests(ProductionApiFixture api)
{
    private const string Auth = "/api/customers/auth";
    private const string Password = "Strong1Pass";
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private WebApplicationFactory<Program> RealSignIn() => api.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Customers:Auth:UseRealSignIn", "true");
        builder.UseSetting("Customers:Auth:AllowInsecureCookies", "true");
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestClientIpStartupFilter>());
    });

    private static HttpClient Browser(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.Header, $"198.18.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}");
        return client;
    }

    private static string NewEmail() => $"http.{Guid.NewGuid():N}@example.test";

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? origin = null, string? csrf = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Auth + path) { Content = JsonContent.Create(body, options: Json) };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (csrf is not null)
        {
            request.Headers.Add(CustomerCsrfFilter.HeaderName, csrf);
        }

        return client.SendAsync(request);
    }

    private static object Registration(string email, string password = Password) =>
        new { email, firstName = "Ayesha", lastName = "Khan", mobile = "0300 1234567", password, keepSignedIn = false };

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static string[] SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

    private static string LinkTokenFor(WebApplicationFactory<Program> factory, string to, string path)
    {
        var sender = factory.Services.GetRequiredService<FakeEmailSender>();
        var message = sender.Sent.Last(m => m.To == to && m.TextBody.Contains(path, StringComparison.Ordinal));
        return Regex.Match(message.TextBody, @"token=([^\s&]+)").Groups[1].Value;
    }

    [Fact]
    [Trait("Spec", "ACT-1")]
    [Trait("Spec", "ACT-11")]
    public async Task Register_SignsTheCustomerIn_WithHardenedCookies_AndNoStore()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();

        var response = await Post(browser, "/register", Registration(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        var cookies = SetCookies(response);
        foreach (var name in new[] { "rc-access=", "rc-refresh=" })
        {
            var cookie = Assert.Single(cookies, c => c.StartsWith(name, StringComparison.Ordinal));
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        }

        var me = await browser.GetAsync("/api/customers/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await BodyAsync(me);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.False(body.GetProperty("emailVerified").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "ACT-18")]
    public async Task WithoutACookie_TheCustomerRoutesAreUnauthorized_AndTheStandInIsNotLettingAnyoneIn()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/customers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/customers/me/addresses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync(Auth + "/session")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ACT-3")]
    public async Task Register_AnEmailInUse_IsConflict_AndAWeakPasswordNamesTheRules()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();
        await Post(browser, "/register", Registration(email));

        using var other = Browser(factory);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(other, "/register", Registration(email.ToUpperInvariant()))).StatusCode);

        var weak = await Post(other, "/register", Registration(NewEmail(), "alllowercase"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        var rules = (await BodyAsync(weak)).GetProperty("unmetRules").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("CAPITAL", rules);
        Assert.Contains("DIGIT", rules);
    }

    [Fact]
    [Trait("Spec", "ACT-5")]
    public async Task SignIn_TheSameAnswerForAnUnknownEmailAndAWrongPassword()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();
        await Post(browser, "/register", Registration(email));
        using var visitor = Browser(factory);

        var wrong = await Post(visitor, "/sign-in", new { email, password = "Wrong1Pass" });
        var unknown = await Post(visitor, "/sign-in", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var (wrongBody, unknownBody) = (await BodyAsync(wrong), await BodyAsync(unknown));
        Assert.Equal(wrongBody.GetProperty("title").GetString(), unknownBody.GetProperty("title").GetString());
        Assert.Equal(wrongBody.GetProperty("status").GetInt32(), unknownBody.GetProperty("status").GetInt32());
        Assert.Empty(SetCookies(wrong));
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    public async Task SignIn_AfterRepeatedFailures_IsDelayedWithRetryAfter_AndNeverLocked()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();
        await Post(browser, "/register", Registration(email));
        using var attacker = Browser(factory);

        for (var i = 0; i < 3; i++)
        {
            await Post(attacker, "/sign-in", new { email, password = "Wrong1Pass" });
        }

        var delayed = await Post(attacker, "/sign-in", new { email, password = Password, captchaToken = FakeCaptchaVerifier.PassToken });

        Assert.Equal(HttpStatusCode.TooManyRequests, delayed.StatusCode);
        Assert.True(delayed.Headers.TryGetValues("Retry-After", out var retry) && int.Parse(retry.Single(), System.Globalization.CultureInfo.InvariantCulture) >= 1);
        Assert.True((await BodyAsync(delayed)).GetProperty("captchaRequired").GetBoolean());

        // The real owner, from another address, is not affected by the attacker's address.
        using var owner = Browser(factory);
        Assert.Equal(HttpStatusCode.OK, (await Post(owner, "/sign-in", new { email, password = Password })).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    public async Task SignIn_FromAnotherSite_IsRefused_ByTheOriginCheck()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);

        var foreign = await Post(browser, "/sign-in", new { email = NewEmail(), password = Password }, origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    [Trait("Spec", "SEC-03")]
    public async Task AnUnsafeRequestFromASignedInCustomer_NeedsTheAntiForgeryHeader()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();
        var registered = await Post(browser, "/register", Registration(email));
        var csrf = (await BodyAsync(registered)).GetProperty("csrfToken").GetString()!;
        var change = new { currentPassword = Password, newPassword = "Brand2NewPass" };

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(browser, "/password/change", change)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(browser, "/password/change", change, csrf: "forged")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(browser, "/password/change", change, csrf: csrf)).StatusCode);

        var session = await browser.GetAsync(Auth + "/session");
        Assert.Equal(csrf, (await BodyAsync(session)).GetProperty("csrfToken").GetString());
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task Refresh_RotatesTheCookies_AndSignOutEndsTheSession()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var registered = await Post(browser, "/register", Registration(NewEmail()));
        var firstAccess = SetCookies(registered).Single(c => c.StartsWith("rc-access=", StringComparison.Ordinal)).Split(';')[0];

        var refreshed = await Post(browser, "/refresh", new { });
        Assert.Equal(HttpStatusCode.NoContent, refreshed.StatusCode);
        var secondAccess = SetCookies(refreshed).Single(c => c.StartsWith("rc-access=", StringComparison.Ordinal)).Split(';')[0];
        Assert.NotEqual(firstAccess, secondAccess);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/customers/me")).StatusCode);

        var signedOut = await Post(browser, "/sign-out", new { });
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/customers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(browser, "/refresh", new { })).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    public async Task VerificationLink_MarksTheEmailVerified_Once()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();
        await Post(browser, "/register", Registration(email));
        var token = LinkTokenFor(factory, email, "verify-email");

        using var visitor = Browser(factory); // the link can be opened on another device
        Assert.Equal(HttpStatusCode.NoContent, (await Post(visitor, "/email/verify", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(visitor, "/email/verify", new { token })).StatusCode);

        Assert.True((await BodyAsync(await browser.GetAsync(Auth + "/session"))).GetProperty("emailVerified").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "ACT-6")]
    [Trait("Spec", "ACT-7")]
    public async Task PasswordReset_IsTheSameAnswerForAnyEmail_AndTheLinkSetsANewPassword()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var email = NewEmail();
        await Post(browser, "/register", Registration(email));
        using var visitor = Browser(factory);

        var known = await Post(visitor, "/password-reset/request", new { email });
        var unknown = await Post(visitor, "/password-reset/request", new { email = NewEmail() });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        var token = LinkTokenFor(factory, email, "reset-password");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(visitor, "/password-reset/confirm", new { token, newPassword = "weak" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(visitor, "/password-reset/confirm", new { token, newPassword = "Brand2NewPass" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(visitor, "/password-reset/confirm", new { token, newPassword = "Brand3NewPass" })).StatusCode);

        // The reset signed the customer out everywhere, and the new password works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/customers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(visitor, "/sign-in", new { email, password = "Brand2NewPass" })).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ACT-2")]
    public async Task PasswordRules_ArePublic_AndListedByName()
    {
        using var factory = RealSignIn();
        using var visitor = Browser(factory);

        var response = await visitor.GetAsync(Auth + "/password-rules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await BodyAsync(response)).EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToArray();
        Assert.Equal(["LENGTH", "CAPITAL", "SMALL", "DIGIT", "NO_SPACES"], codes);
    }

    [Fact]
    [Trait("Spec", "SEC-25")]
    public async Task NoResponse_EverContainsThePasswordOrTheCookieValues()
    {
        using var factory = RealSignIn();
        using var browser = Browser(factory);
        var registered = await Post(browser, "/register", Registration(NewEmail()));
        var text = await registered.Content.ReadAsStringAsync();
        var access = SetCookies(registered).Single(c => c.StartsWith("rc-access=", StringComparison.Ordinal)).Split(';')[0]["rc-access=".Length..];

        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
        Assert.DoesNotContain(access, text, StringComparison.Ordinal);
    }

    /// <summary>Lets a test choose the address the host sees for a request, because the test server has none.</summary>
    private sealed class TestClientIpStartupFilter : IStartupFilter
    {
        public const string Header = "X-Test-Client-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out var value) && IPAddress.TryParse(value.ToString(), out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                await continuation(context);
            });
            next(app);
        };
    }
}
