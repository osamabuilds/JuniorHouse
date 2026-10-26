using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Infrastructure.Sessions;

namespace Romp.Modules.Identity.Tests.Sessions;

public sealed class CsrfTokenServiceTests
{
    private static CsrfTokenService Service(string? key = null) =>
        new(Options.Create(new CsrfOptions { Key = key ?? Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()) }));

    [Fact]
    [Trait("Spec", "AC-10")]
    public void TheValue_IsStablePerSession_AndDiffersBetweenSessions()
    {
        var service = Service();

        Assert.Equal(service.Create(7), service.Create(7));
        Assert.NotEqual(service.Create(7), service.Create(8));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", service.Create(7)); // 32 bytes, base64url
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void TheValue_DependsOnTheServerKey()
    {
        var other = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 100)).ToArray());

        Assert.NotEqual(Service().Create(7), Service(other).Create(7));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void IsValid_AcceptsOnlyTheSessionsOwnValue()
    {
        var service = Service();
        var mine = service.Create(7);

        Assert.True(service.IsValid(7, mine));
        Assert.False(service.IsValid(8, mine));
        Assert.False(service.IsValid(7, null));
        Assert.False(service.IsValid(7, ""));
        Assert.False(service.IsValid(7, mine[..^1]));          // shorter
        Assert.False(service.IsValid(7, mine + "A"));          // longer
        Assert.False(service.IsValid(7, mine.ToUpperInvariant())); // base64url is case sensitive
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void WithoutAConfiguredKey_ADevelopmentProcessGetsItsOwnRandomOne()
    {
        var first = new CsrfTokenService(Options.Create(new CsrfOptions()));
        var second = new CsrfTokenService(Options.Create(new CsrfOptions()));

        Assert.NotEqual(first.Create(7), second.Create(7));
    }
}

public sealed class CsrfOptionsValidatorTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ValidateOptionsResult Validate(string environment, string? key) =>
        new CsrfOptionsValidator(new Env(environment)).Validate(null, new CsrfOptions { Key = key });

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData("Production", null)]
    [InlineData("Production", "")]
    [InlineData("Production", "not base64 !!")]
    [InlineData("Staging", "c2hvcnQ=")] // valid base64 but only 5 bytes
    public void OutsideDevelopment_AStrongKeyIsRequired(string environment, string? key)
    {
        Assert.True(Validate(environment, key).Failed);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void OutsideDevelopment_A32ByteKeyIsAccepted()
    {
        Assert.True(Validate("Production", Convert.ToBase64String(new byte[32])).Succeeded);
    }

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void InDevelopmentAndTesting_NoKeyIsFine(string environment)
    {
        Assert.True(Validate(environment, null).Succeeded);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void AMalformedKey_IsRefusedEverywhere()
    {
        Assert.True(Validate("Development", "not base64 !!").Failed);
    }
}

public sealed class CsrfFilterTests
{
    private static readonly CsrfTokenService Tokens = new(Options.Create(new CsrfOptions { Key = Convert.ToBase64String(new byte[32]) }));

    private static DefaultHttpContext Request(string method, long? sessionId = null, string? header = null, string? origin = null, string host = "romp.pk")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        if (header is not null)
        {
            context.Request.Headers[CsrfFilter.HeaderName] = header;
        }

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (sessionId is { } id)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim("sid", id.ToString(System.Globalization.CultureInfo.InvariantCulture))], "test"));
        }

        return context;
    }

    private static async Task<(bool Reached, int? Status)> RunAsync(IEndpointFilter filter, HttpContext context)
    {
        var reached = false;
        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ =>
            {
                reached = true;
                return ValueTask.FromResult<object?>(Results.Ok());
            });
        return (reached, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task UnsafeRequests_NeedTheHeader(string method)
    {
        var filter = new CsrfFilter(Tokens);

        var missing = await RunAsync(filter, Request(method, sessionId: 7));
        Assert.Equal((false, 403), missing);

        var wrong = await RunAsync(filter, Request(method, 7, header: "definitely-wrong"));
        Assert.Equal((false, 403), wrong);

        var otherSessions = await RunAsync(filter, Request(method, 7, header: Tokens.Create(8)));
        Assert.Equal((false, 403), otherSessions);

        var empty = await RunAsync(filter, Request(method, 7, header: ""));
        Assert.Equal((false, 403), empty);

        var good = await RunAsync(filter, Request(method, 7, header: Tokens.Create(7)));
        Assert.True(good.Reached);
    }

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeRequests_NeedNoHeader(string method)
    {
        var result = await RunAsync(new CsrfFilter(Tokens), Request(method, sessionId: 7));

        Assert.True(result.Reached);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task ARequestWithoutASession_IsLeftToTheAuthenticationAndOriginChecks()
    {
        // Sign-in carries no session yet; it is guarded by SameSite=Strict and the Origin check instead.
        var result = await RunAsync(new CsrfFilter(Tokens), Request("POST"));

        Assert.True(result.Reached);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task TheRefusal_DoesNotSayWhatWasWrong()
    {
        var context = Request("POST", sessionId: 7, header: "wrong");
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();

        var result = await new CsrfFilter(Tokens).InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context), _ => ValueTask.FromResult<object?>(null));
        await ((IResult)result!).ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain(Tokens.Create(7), body, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(403, context.Response.StatusCode);
    }

    private static OriginCheckFilter Origin(string? publicBaseUrl) =>
        new(Options.Create(new SiteOptions { PublicBaseUrl = publicBaseUrl }));

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task WrongOrigin_IsRejected()
    {
        var filter = Origin("https://romp.pk");

        foreach (var origin in new[]
                 {
                     "https://evil.example",
                     "https://romp.pk.evil.example", // the allowed name as a prefix
                     "https://evil.example/https://romp.pk",
                     "http://romp.pk",               // wrong scheme
                     "https://romp.pk:8443",         // wrong port
                     "https://sub.romp.pk",          // a sibling is not the site
                     "null",                         // sandboxed frames and redirects send this
                     "not a url",
                 })
        {
            var result = await RunAsync(filter, Request("POST", origin: origin));
            Assert.Equal((false, 403), result);
        }
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task TheSitesOwnOrigin_IsAccepted_InAnyCase_AndANonBrowserWithoutOneIsLeftAlone()
    {
        var filter = Origin("https://romp.pk");

        Assert.True((await RunAsync(filter, Request("POST", origin: "https://romp.pk"))).Reached);
        Assert.True((await RunAsync(filter, Request("POST", origin: "https://ROMP.PK"))).Reached);
        Assert.True((await RunAsync(filter, Request("POST"))).Reached); // curl, tests: no Origin, so not a cross-site browser request
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task WithoutAConfiguredSite_OnlyTheRequestsOwnOriginIsAccepted()
    {
        var filter = Origin(null);

        Assert.True((await RunAsync(filter, Request("POST", origin: "https://localhost:5051", host: "localhost:5051"))).Reached);
        Assert.Equal((false, 403), await RunAsync(filter, Request("POST", origin: "https://evil.example", host: "localhost:5051")));
    }
}
