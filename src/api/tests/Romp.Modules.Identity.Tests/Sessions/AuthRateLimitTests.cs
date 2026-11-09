using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Infrastructure.Sessions;

namespace Romp.Modules.Identity.Tests.Sessions;

/// <summary>Sessions task 11 (AC-12): every route under /api/auth is held to the strict per-address limit.</summary>
public sealed class AuthRateLimitTests
{
    private const int Strict = 3;

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimit:StrictPerMinute"] = Strict.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimit:PerMinute"] = "1000",
        });
        builder.Services.AddRompRateLimiting(builder.Configuration);
        builder.Services.AddRompAuthorization(builder.Environment);

        var app = builder.Build();
        app.Use((context, next) =>
        {
            // Stands in for the forwarded-headers middleware: the test says which address the request came from.
            context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Ip"].ToString());
            return next(context);
        });
        app.UseRateLimiter();
        app.UseAuthorization();
        var auth = app.MapAuthRoutes();
        auth.MapPost("/sign-in", () => Results.Ok());
        auth.MapPost("/refresh", () => Results.Ok());
        app.MapPost("/api/other", () => Results.Ok());
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, string path, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Test-Ip", ip);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task StrictPolicy_Applies()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        for (var i = 0; i < Strict; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await PostAsync(client, "/api/auth/sign-in", "203.0.113.20"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(client, "/api/auth/sign-in", "203.0.113.20"));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task TheLimit_IsPerAddress_AndSharedByEveryAuthRoute()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        await PostAsync(client, "/api/auth/sign-in", "203.0.113.21");
        await PostAsync(client, "/api/auth/refresh", "203.0.113.21");
        await PostAsync(client, "/api/auth/sign-in", "203.0.113.21");

        Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(client, "/api/auth/refresh", "203.0.113.21")); // one bucket for the whole group
        Assert.Equal(HttpStatusCode.OK, await PostAsync(client, "/api/auth/sign-in", "203.0.113.22")); // another visitor is unaffected
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task RoutesOutsideTheAuthGroup_KeepTheGenerousDefault()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        for (var i = 0; i < Strict * 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await PostAsync(client, "/api/other", "203.0.113.23"));
        }
    }
}
