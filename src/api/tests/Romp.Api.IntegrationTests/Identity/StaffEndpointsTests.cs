using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Staff accounts through the real HTTP API against a real PostgreSQL (staff-accounts step 9): permission checks, the one-time
/// password responses, duplicate names, and the own-password endpoint. Sessions do not exist yet, so a test scheme stands in for
/// the signed-in user: it reads the account id from a header and puts it in the claims the sessions feature will set.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class StaffEndpointsTests(ProductionApiFixture api)
{
    private const string TestScheme = "TestSession";
    private const string UserHeader = "X-Test-User";

    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private static readonly int[] CatalogueRole = [2];

    private static string NewName() => $"http.{Guid.NewGuid():N}"[..24];

    private HttpClient ClientWhere(Action<IServiceCollection> configure) =>
        api.Factory.WithWebHostBuilder(b => b.ConfigureServices(configure)).CreateClient();

    private HttpClient AsSignedInUser() => ClientWhere(services =>
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderSessionHandler>(TestScheme, _ => { });
        services.Configure<AuthenticationOptions>(o => o.DefaultScheme = TestScheme);
    });

    private async Task<(long Id, string Name, string Password)> CreateAsync(HttpClient? client = null)
    {
        var name = NewName();
        var response = await (client ?? api.Client).PostAsJsonAsync("/api/identity/staff", new { userName = name, displayName = "Http Person", roleIds = CatalogueRole }, Json);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode} {body}");
        var json = JsonSerializer.Deserialize<JsonElement>(body, Json);
        return (json.GetProperty("userId").GetInt64(), name, json.GetProperty("temporaryPassword").GetString()!);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task WithoutPermission_Returns403()
    {
        using var client = ClientWhere(services => services.AddSingleton<IAuthorizationHandler>(new DenyStaffManage()));

        var calls = new[]
        {
            await client.GetAsync("/api/identity/staff"),
            await client.GetAsync("/api/identity/staff/1"),
            await client.PostAsJsonAsync("/api/identity/staff", new { userName = "someone.new", displayName = "X", roleIds = CatalogueRole }, Json),
            await client.PostAsync("/api/identity/staff/1/deactivate", null),
            await client.PostAsync("/api/identity/staff/1/reactivate", null),
            await client.PostAsync("/api/identity/staff/1/reset-password", null),
        };

        Assert.All(calls, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task OneTimePassword_IsReturnedOnce()
    {
        var (id, _, password) = await CreateAsync();

        var created = await api.Client.PostAsJsonAsync("/api/identity/staff", new { userName = NewName(), displayName = "Second", roleIds = CatalogueRole }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", string.Join(",", created.Headers.CacheControl?.ToString()), StringComparison.Ordinal);

        var detail = await api.Client.GetStringAsync($"/api/identity/staff/{id}");
        var list = await api.Client.GetStringAsync("/api/identity/staff?pageSize=100");
        Assert.DoesNotContain(password, detail, StringComparison.Ordinal);
        Assert.DoesNotContain(password, list, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", detail, StringComparison.OrdinalIgnoreCase);

        var reset = await api.Client.PostAsync($"/api/identity/staff/{id}/reset-password", null);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains("no-store", reset.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        var next = JsonSerializer.Deserialize<JsonElement>(await reset.Content.ReadAsStringAsync(), Json).GetProperty("temporaryPassword").GetString();
        Assert.NotEqual(password, next);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Create_WithATakenOrInvalidName_Returns409Or400()
    {
        var (_, name, _) = await CreateAsync();

        var duplicate = await api.Client.PostAsJsonAsync("/api/identity/staff", new { userName = name.ToUpperInvariant(), displayName = "Dup", roleIds = CatalogueRole }, Json);
        var invalid = await api.Client.PostAsJsonAsync("/api/identity/staff", new { userName = "Bad Name!", displayName = "Bad", roleIds = CatalogueRole }, Json);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task DeactivateAndReactivate_AreIdempotent_AndAnUnknownIdIs404()
    {
        var (id, _, _) = await CreateAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.PostAsync($"/api/identity/staff/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.PostAsync($"/api/identity/staff/{id}/deactivate", null)).StatusCode);
        Assert.False(JsonSerializer.Deserialize<JsonElement>(await api.Client.GetStringAsync($"/api/identity/staff/{id}"), Json).GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.PostAsync($"/api/identity/staff/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PostAsync("/api/identity/staff/999999999/deactivate", null)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void CreateAndReset_AreUnderTheStrictRateLimit()
    {
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true)
            .ToDictionary(e => e.RoutePattern.RawText ?? string.Empty, e => e.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);

        foreach (var pattern in new[] { "/api/identity/staff/", "/api/identity/staff/{id:long}/reset-password", "/api/auth/password" })
        {
            Assert.True(endpoints.TryGetValue(pattern, out var policy), $"no POST route {pattern}; staff/auth routes: {string.Join(" | ", endpoints.Keys.Where(k => k.Contains("staff", StringComparison.Ordinal) || k.Contains("auth", StringComparison.Ordinal)))}");
            Assert.Equal(RateLimitPolicies.Strict, policy);
        }
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task ChangeOwnPassword_NeedsASession_AndChecksTheCurrentPassword()
    {
        var (id, _, temporary) = await CreateAsync();
        const string Fresh = "correct-horse-battery-staple-91";

        var anonymous = await api.Client.PostAsJsonAsync("/api/auth/password", new { currentPassword = temporary, newPassword = Fresh }, Json);
        using var signedIn = AsSignedInUser();
        signedIn.DefaultRequestHeaders.Add(UserHeader, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var wrong = await signedIn.PostAsJsonAsync("/api/auth/password", new { currentPassword = "not-the-password-0000", newPassword = Fresh }, Json);
        var weak = await signedIn.PostAsJsonAsync("/api/auth/password", new { currentPassword = temporary, newPassword = "short" }, Json);
        var ok = await signedIn.PostAsJsonAsync("/api/auth/password", new { currentPassword = temporary, newPassword = Fresh }, Json);
        var again = await signedIn.PostAsJsonAsync("/api/auth/password", new { currentPassword = Fresh, newPassword = temporary }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode); // the temporary password was the previous one
    }

    private sealed class DenyStaffManage : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            context.Fail();
            return Task.CompletedTask;
        }
    }

    private sealed class HeaderSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var id))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], TestScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
        }
    }
}
