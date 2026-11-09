using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Mfa;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Two-step verification through the real HTTP API against a real PostgreSQL (mfa task 12): enrolment, the second step of sign-in with
/// an authenticator code and with a recovery code, the scope rules, and the administrator's reset. Time is real, so codes are worked out
/// from the secret the way an authenticator app would.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class MfaEndpointsTests(ProductionApiFixture api)
{
    private const string Own = "Correct-Horse-Battery-Staple-42";
    private const string CsrfHeader = "X-Romp-Csrf";

    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;
    private static readonly short[] CatalogueRole = [2];

    private WebApplicationFactory<Program> RealAuth() => api.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Identity:UseRealAuthentication", "true");
        builder.UseSetting("Identity:AllowInsecureCookies", "true");
    });

    private static HttpClient Browser(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private async Task<(string Name, string Temporary, long UserId)> NewAccountAsync()
    {
        using var scope = api.Services.CreateScope();
        var name = $"mfa.{Guid.NewGuid():N}"[..22];
        var created = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateStaffCommand(name, "Mfa Person", CatalogueRole));
        return (name, created.TemporaryPassword.Reveal(), created.UserId);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static HttpRequestMessage Post(string path, string csrf, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        request.Headers.Add(CsrfHeader, csrf);
        return request;
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string name, string password) =>
        await client.PostAsJsonAsync("/api/auth/sign-in", new { userName = name, password }, Json);

    private static async Task<string> CsrfAsync(HttpClient client) =>
        (await BodyAsync(await client.GetAsync("/api/auth/me"))).GetProperty("csrfToken").GetString()!;

    private static string Code(byte[] secret, int stepOffset = 0) => Totp.Code(secret, Totp.StepOf(DateTimeOffset.UtcNow) + stepOffset);

    /// <summary>A browser for a new account that has chosen its own password; it holds a limited session (scope EnrolMfa).</summary>
    private async Task<(HttpClient Browser, string Name, long UserId, WebApplicationFactory<Program> Factory)> ReadyToEnrolAsync()
    {
        var factory = RealAuth();
        var browser = Browser(factory);
        var (name, temporary, userId) = await NewAccountAsync();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(browser, name, temporary)).StatusCode);
        var change = Post("/api/auth/password", await CsrfAsync(browser), new { currentPassword = temporary, newPassword = Own });
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(change)).StatusCode);
        return (browser, name, userId, factory);
    }

    private static async Task<(byte[] Secret, string[] Codes)> EnrolAsync(HttpClient browser)
    {
        var csrf = await CsrfAsync(browser);
        var started = await browser.SendAsync(Post("/api/auth/mfa/enrol/start", csrf));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal("no-store", started.Headers.CacheControl?.ToString());
        var secret = Base32.Decode((await BodyAsync(started)).GetProperty("secretText").GetString()!);

        var confirmed = await browser.SendAsync(Post("/api/auth/mfa/enrol/confirm", csrf, new { code = Code(secret) }));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("no-store", confirmed.Headers.CacheControl?.ToString());
        var codes = (await BodyAsync(confirmed)).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        return (secret, codes);
    }

    private static async Task<HttpResponseMessage> VerifyAsync(HttpClient browser, string code) =>
        await browser.PostAsJsonAsync("/api/auth/mfa/verify", new { code }, Json);

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-2")]
    [Trait("Spec", "AC-3")]
    public async Task FullEnrolmentAndSignIn()
    {
        var (browser, name, _, factory) = await ReadyToEnrolAsync();
        using var _ = factory;
        using var __ = browser;
        Assert.Equal("EnrolMfa", (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("scope").GetString());

        var (secret, codes) = await EnrolAsync(browser);

        // Enrolling moved the same session to the full scope; recovery codes were shown once.
        Assert.Equal(10, codes.Length);
        Assert.Equal("Full", (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("scope").GetString());
        var status = await BodyAsync(await browser.GetAsync("/api/auth/mfa"));
        Assert.True(status.GetProperty("enrolled").GetBoolean());
        Assert.Equal(10, status.GetProperty("recoveryCodesRemaining").GetInt32());
        Assert.DoesNotContain("secret", status.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // Sign out and back in: the password step now only gets a pending cookie, and no session.
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/auth/sign-out", null)).StatusCode);
        var password = await SignInAsync(browser, name, Own);
        Assert.Equal("mfa_required", (await BodyAsync(password)).GetProperty("status").GetString());
        var cookies = password.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Single(cookies);
        Assert.Contains("romp-pnd=", cookies[0], StringComparison.Ordinal);
        Assert.Contains("path=/api/auth/mfa", cookies[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);

        // The code that confirmed enrolment is spent; the next step's code is accepted (one step of drift).
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(browser, Code(secret))).StatusCode);
        var verified = await VerifyAsync(browser, Code(secret, 1));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal("signed_in", (await BodyAsync(verified)).GetProperty("status").GetString());
        Assert.Equal("no-store", verified.Headers.CacheControl?.ToString());
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"), c => c.StartsWith("romp-acs=", StringComparison.Ordinal));
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"), c => c.StartsWith("romp-pnd=;", StringComparison.Ordinal)); // the pending cookie is cleared
        Assert.Equal("Full", (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("scope").GetString());
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task ARecoveryCode_SignsIn_Once()
    {
        var (browser, name, _, factory) = await ReadyToEnrolAsync();
        using var _ = factory;
        using var __ = browser;
        var (_, codes) = await EnrolAsync(browser);
        await browser.PostAsync("/api/auth/sign-out", null);

        await SignInAsync(browser, name, Own);
        var first = await VerifyAsync(browser, codes[0]);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(9, (await BodyAsync(await browser.GetAsync("/api/auth/mfa"))).GetProperty("recoveryCodesRemaining").GetInt32());

        await browser.PostAsync("/api/auth/sign-out", null);
        await SignInAsync(browser, name, Own);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(browser, codes[0])).StatusCode); // already used
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task AWrongCode_GivesOneGenericAnswer_AndNoSession()
    {
        var (browser, name, _, factory) = await ReadyToEnrolAsync();
        using var _ = factory;
        using var __ = browser;
        var (secret, _) = await EnrolAsync(browser);
        await browser.PostAsync("/api/auth/sign-out", null);
        await SignInAsync(browser, name, Own);

        var wrong = await VerifyAsync(browser, Code(secret, 7));

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var title = (await BodyAsync(wrong)).GetProperty("title").GetString();
        Assert.DoesNotContain("replay", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expired", title, StringComparison.OrdinalIgnoreCase);
        Assert.False(wrong.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("romp-acs=", StringComparison.Ordinal)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task TheScopeRules_KeepEachStepToItsOwnSession()
    {
        var (browser, _, _, factory) = await ReadyToEnrolAsync();
        using var _ = factory;
        using var __ = browser;
        var csrf = await CsrfAsync(browser);

        // A session that must still enrol cannot regenerate recovery codes (there are none).
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(Post("/api/auth/mfa/recovery-codes", csrf, new { code = "123456" }))).StatusCode);

        var (secret, _) = await EnrolAsync(browser);

        // A full session cannot start a second enrolment, and regenerating needs a current code of a newer step.
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(Post("/api/auth/mfa/enrol/start", csrf))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(Post("/api/auth/mfa/recovery-codes", csrf, new { code = Code(secret) }))).StatusCode);
        var fresh = await browser.SendAsync(Post("/api/auth/mfa/recovery-codes", csrf, new { code = Code(secret, 1) }));
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(10, (await BodyAsync(fresh)).GetProperty("recoveryCodes").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task WithoutASession_EveryEnrolmentRouteReturns401_AndUnsafeOnesNeedTheHeader()
    {
        using var factory = RealAuth();
        using var plain = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        foreach (var path in new[] { "/api/auth/mfa/enrol/start", "/api/auth/mfa/enrol/confirm", "/api/auth/mfa/recovery-codes" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await plain.PostAsJsonAsync(path, new { code = "123456" }, Json)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await plain.GetAsync("/api/auth/mfa")).StatusCode);

        // With a session but without the anti-forgery header the enrolment routes refuse (AC-10 of sessions).
        var (browser, _, _, own) = await ReadyToEnrolAsync();
        using var _ = own;
        using var __ = browser;
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsync("/api/auth/mfa/enrol/start", null)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task ResetMfa_NeedsThePermission_AndForcesReEnrolment()
    {
        var (browser, name, userId, factory) = await ReadyToEnrolAsync();
        using var _ = factory;
        using var __ = browser;
        await EnrolAsync(browser);

        // Without the staff.manage permission: 403. The default host's development user holds every permission.
        using (var denied = api.Factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IAuthorizationHandler>(new DenyAll()))).CreateClient())
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsync($"/api/identity/staff/{userId}/reset-mfa", null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.PostAsync($"/api/identity/staff/{userId}/reset-mfa", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PostAsync("/api/identity/staff/999999999/reset-mfa", null)).StatusCode);

        // Their sessions ended, and the next sign-in only gets a session that can enrol again.
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
        var again = await SignInAsync(browser, name, Own);
        Assert.Equal("limited", (await BodyAsync(again)).GetProperty("status").GetString());
        Assert.Equal("EnrolMfa", (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("scope").GetString());
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public void StrictRateLimitApplies()
    {
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/auth/mfa", StringComparison.Ordinal) == true)
            .ToList();

        Assert.True(endpoints.Count >= 5, "the MFA endpoints were not found");
        Assert.All(endpoints, e =>
            Assert.Equal(RateLimitPolicies.Strict, e.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));

        var reset = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText?.EndsWith("/reset-mfa", StringComparison.Ordinal) == true);
        Assert.Equal(RateLimitPolicies.Strict, reset.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    private sealed class DenyAll : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            context.Fail();
            return Task.CompletedTask;
        }
    }
}
