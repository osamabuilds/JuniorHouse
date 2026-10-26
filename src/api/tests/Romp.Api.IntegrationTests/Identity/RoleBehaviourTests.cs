using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Roles R10 (AC-11, AC-14, AC-15): with real authentication on, a signed-in user of each role calls every route of the authorization
/// matrix. A route the role's permissions cover must not answer 401 or 403; a route they do not cover answers 403; with no session every
/// protected route answers 401. The expected answers come from the matrix and the role-permission data, so a new route needs only a
/// matrix line.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class RoleBehaviourTests(ProductionApiFixture api)
{
    private const string Own = "Correct-Horse-Battery-Staple-42";

    /// <summary>Routes that are not called for real: they start background work or change people's accounts.</summary>
    private static readonly string[] NotCalled =
    [
        "/api/identity/",
        "/rebuild",
        "/storefront-settings/publish",
        "/storefront-settings/restore",
    ];

    private WebApplicationFactory<Program> RealAuth() => api.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Identity:UseRealAuthentication", "true");
        builder.UseSetting("Identity:AllowInsecureCookies", "true");
    });

    private sealed record Person(string Role, HttpClient Browser, string Csrf, long UserId, IReadOnlySet<string> Permissions);

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);

    /// <summary>A signed-in person with a full session: new account, own password, two-step set up, then signed in again.</summary>
    private async Task<Person> SignedInAsync(WebApplicationFactory<Program> factory, string roleCode, short roleId)
    {
        var name = $"role.{Guid.NewGuid():N}"[..22];
        using var scope = api.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var created = await sender.Send(new CreateStaffCommand(name, "Role Person", [roleId]));
        var permissions = await sender.Send(new GetEffectivePermissionsQuery(created.UserId));

        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await browser.PostAsJsonAsync("/api/auth/sign-in", new { userName = name, password = created.TemporaryPassword.Reveal() }, ProductionApiFixture.Json);
        var csrf = (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("csrfToken").GetString()!;
        HttpRequestMessage Post(string path, object? body = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body, options: ProductionApiFixture.Json) };
            request.Headers.Add("X-Romp-Csrf", csrf);
            return request;
        }

        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(Post("/api/auth/password", new { currentPassword = created.TemporaryPassword.Reveal(), newPassword = Own }))).StatusCode);
        var started = await BodyAsync(await browser.SendAsync(Post("/api/auth/mfa/enrol/start")));
        var secret = Base32.Decode(started.GetProperty("secretText").GetString()!);
        var confirmed = await browser.SendAsync(Post("/api/auth/mfa/enrol/confirm", new { code = Totp.Code(secret, Totp.StepOf(DateTimeOffset.UtcNow)) }));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        Assert.Equal("Full", (await BodyAsync(await browser.GetAsync("/api/auth/me"))).GetProperty("scope").GetString());
        return new Person(roleCode, browser, csrf, created.UserId, permissions);
    }

    private static string Concrete(string route) =>
        Regex.Replace(route, @"\{[^}:]+:long\}", "999999999").Replace("{no:int}", "1", StringComparison.Ordinal) is var r
            ? Regex.Replace(r, @"\{[^}]+\}", "1")
            : route;

    private static async Task<HttpStatusCode> CallAsync(Person? person, HttpClient fallback, MatrixRow row)
    {
        var path = Concrete(row.Route);
        using var request = new HttpRequestMessage(new HttpMethod(row.Method == "ANY" ? "GET" : row.Method), path);
        if (row.Method == "POST" && (row.Route.EndsWith("/files", StringComparison.Ordinal) || row.Route.EndsWith("/images", StringComparison.Ordinal)))
        {
            // Upload routes only match a multipart request; without it the framework answers 415 before authorization is reached.
            request.Content = new MultipartFormDataContent();
        }
        else if (row.Method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        if (person is not null && row.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            request.Headers.Add("X-Romp-Csrf", person.Csrf);
        }

        using var response = await (person?.Browser ?? fallback).SendAsync(request);
        return response.StatusCode;
    }

    private static IReadOnlyList<MatrixRow> Protected(IReadOnlyList<MatrixRow> matrix) =>
        [.. matrix.Where(r => r.Permission is not (MatrixRow.Anonymous or MatrixRow.Session) && !NotCalled.Any(n => r.Route.Contains(n, StringComparison.Ordinal)))];

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task EveryRole_GetsExactlyWhatTheMatrixAllows()
    {
        var matrix = Protected(AuthorizationMatrix.ParseCsv(File.ReadAllText(MatrixFile())));
        using var factory = RealAuth();
        var roles = new Dictionary<string, short>();
        using (var scope = api.Services.CreateScope())
        {
            var list = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ListRolesQuery());
            foreach (var role in list)
            {
                roles[role.Code] = role.Id;
            }
        }

        var people = new List<Person>();
        foreach (var code in new[] { "OWNER", "CATALOGUE", "ORDER_OPS", "SUPPORT", "MARKETING" })
        {
            people.Add(await SignedInAsync(factory, code, roles[code]));
        }

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var wrong = new List<string>();
        foreach (var row in matrix)
        {
            // With no session at all: 401, whatever the route.
            var noSession = await CallAsync(null, anonymous, row);
            if (noSession != HttpStatusCode.Unauthorized)
            {
                wrong.Add($"no session {row.Key}: {(int)noSession}, expected 401");
            }

            foreach (var person in people)
            {
                var allowed = person.Permissions.Contains(row.Permission);
                var status = await CallAsync(person, anonymous, row);
                if (allowed && status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    wrong.Add($"{person.Role} {row.Key} ({row.Permission}): {(int)status}, expected it to be allowed");
                }
                else if (!allowed && status != HttpStatusCode.Forbidden)
                {
                    wrong.Add($"{person.Role} {row.Key} ({row.Permission}): {(int)status}, expected 403");
                }
            }
        }

        foreach (var person in people)
        {
            person.Browser.Dispose();
        }

        Assert.True(wrong.Count == 0, $"{wrong.Count} answers differ from the matrix:\n" + string.Join('\n', wrong.Take(40)));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task Me_ListsThePermissionsOfTheCaller_ForHidingMenusOnly()
    {
        using var factory = RealAuth();
        short catalogue;
        using (var scope = api.Services.CreateScope())
        {
            catalogue = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ListRolesQuery())).Single(r => r.Code == "CATALOGUE").Id;
        }

        var person = await SignedInAsync(factory, "CATALOGUE", catalogue);
        using var browser = person.Browser;

        var me = await BodyAsync(await browser.GetAsync("/api/auth/me"));
        var listed = me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["catalog.read", "catalog.write", "ref.read", "warehouse.read"], listed);

        // The development stand-in user holds everything, so the admin app works unchanged without sign-in.
        var dev = await BodyAsync(await api.Client.GetAsync("/api/auth/me"));
        Assert.Equal(Romp.BuildingBlocks.Web.Permissions.All.Count, dev.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task OrderOps_CanViewStock_ButCannotAdjustApproveOrSetThresholds()
    {
        using var factory = RealAuth();
        short orderOps;
        using (var scope = api.Services.CreateScope())
        {
            orderOps = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ListRolesQuery())).Single(r => r.Code == "ORDER_OPS").Id;
        }

        var person = await SignedInAsync(factory, "ORDER_OPS", orderOps);
        using var browser = person.Browser;
        async Task<HttpStatusCode> SendAsync(HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, path) { Content = method == HttpMethod.Get ? null : new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Romp-Csrf", person.Csrf);
            return (await browser.SendAsync(request)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/warehouse/stock/"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/warehouse/stock/movements"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/warehouse/adjustments/"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/warehouse/alerts/"));

        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Post, "/api/warehouse/adjustments/"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Post, "/api/warehouse/cycle-counts/999999999/confirm"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/warehouse/alerts/thresholds/999999999"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Get, "/api/purchase-orders/")); // cost and advance terms are not for this role
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task APermissionCanBeGivenToANewRole_ByInsertingLookupRowsOnly()
    {
        using var factory = RealAuth();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var catalogue = (await sender.Send(new ListRolesQuery())).Single(r => r.Code == "CATALOGUE").Id;
        var person = await SignedInAsync(factory, "CATALOGUE", catalogue);
        using var browser = person.Browser;

        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/purchase-orders/")).StatusCode);

        // A new role is data: one role row, and one mapping row for each permission it should hold. No migration, no code change.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"IDNT\".\"ROLE_LKP\" (\"ID\", \"CODE\", \"NAME\", \"SORT_SEQ\", \"ACT_IND\", \"ALL_PERM_IND\") VALUES (9100, 'TEST_BUYER', 'Test buyer', 900, true, false)");
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"IDNT\".\"ROLE_PERM_MAP\" (\"ROLE_ID\", \"PERM_ID\") SELECT 9100, \"ID\" FROM \"IDNT\".\"PERM_LKP\" WHERE \"CODE\" IN ('procurement.read', 'ref.read')");
            Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/purchase-orders/")).StatusCode); // the person does not have the new role yet

            var admin = api.Client; // the development host stand-in user holds staff.manage; the shared client is not disposed
            var both = new[] { (int)catalogue, 9100 };
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/identity/staff/{person.UserId}/roles", new { roleIds = both }, ProductionApiFixture.Json)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/purchase-orders/")).StatusCode); // next request, same session
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/catalog/styles/")).StatusCode); // the old role still applies: a union
            Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsync("/api/purchase-orders/", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode); // write is not in the new role

            // Taking the role away closes it again on the very next request.
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/identity/staff/{person.UserId}/roles", new { roleIds = new[] { (int)catalogue } }, ProductionApiFixture.Json)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/purchase-orders/")).StatusCode);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IDNT\".\"USR_ROLE_MAP\" WHERE \"ROLE_ID\" = 9100");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IDNT\".\"ROLE_PERM_MAP\" WHERE \"ROLE_ID\" = 9100");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IDNT\".\"ROLE_LKP\" WHERE \"ID\" = 9100");
        }
    }

    private static string MatrixFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "docs", "specs", "identity", "roles", "authorization-matrix.csv");
    }
}
