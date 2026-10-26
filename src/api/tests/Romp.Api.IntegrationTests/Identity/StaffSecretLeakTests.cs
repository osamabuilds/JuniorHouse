using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Staff-accounts step 11: the plain password, the stored hash and the temporary password must not appear in any log line (message,
/// scopes, exception text) or in any error response. The temporary password is allowed in exactly one place, the creation and reset
/// responses, so those bodies are left out of the sweep. The sweep also proves it captured something, so it cannot pass by being empty.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class StaffSecretLeakTests(ProductionApiFixture api)
{
    private const string TestScheme = "TestSession";
    private const string UserHeader = "X-Test-User";
    private const string Fresh = "canary-horse-battery-staple-77";
    private const string Wrong = "canary-wrong-password-0000";

    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private static readonly int[] CatalogueRole = [2];

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task NothingSensitive_AppearsInOutput()
    {
        var sink = new CapturingLoggerProvider();
        using var client = api.Factory.WithWebHostBuilder(b => b.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(sink))).CreateClient();

        var name = $"leak.{Guid.NewGuid():N}"[..22];
        var secrets = new List<string> { Fresh, Wrong };
        var errorBodies = new StringBuilder();

        var created = await client.PostAsJsonAsync("/api/identity/staff", new { userName = name, displayName = "Leak Check", roleIds = CatalogueRole }, Json);
        Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);
        var createdJson = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync(), Json);
        var id = createdJson.GetProperty("userId").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var temporary = createdJson.GetProperty("temporaryPassword").GetString()!;
        secrets.Add(temporary);

        var reset = await client.PostAsync($"/api/identity/staff/{id}/reset-password", null);
        secrets.Add(JsonSerializer.Deserialize<JsonElement>(await reset.Content.ReadAsStringAsync(), Json).GetProperty("temporaryPassword").GetString()!);

        using var signedIn = api.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(sink);
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderSessionHandler>(TestScheme, _ => { });
            services.Configure<AuthenticationOptions>(o => o.DefaultScheme = TestScheme);
        })).CreateClient();
        signedIn.DefaultRequestHeaders.Add(UserHeader, id);

        // Failure paths are where secrets leak: a rejected weak password, a wrong current password, a repeated password.
        foreach (var body in new object[]
        {
            new { currentPassword = secrets[^1], newPassword = "short" },
            new { currentPassword = Wrong, newPassword = Fresh },
            new { currentPassword = secrets[^1], newPassword = name },
        })
        {
            var response = await signedIn.PostAsJsonAsync("/api/auth/password", body, Json);
            errorBodies.Append(await response.Content.ReadAsStringAsync());
        }

        var ok = await signedIn.PostAsJsonAsync("/api/auth/password", new { currentPassword = secrets[^1], newPassword = Fresh }, Json);
        errorBodies.Append(await ok.Content.ReadAsStringAsync());
        var repeat = await signedIn.PostAsJsonAsync("/api/auth/password", new { currentPassword = Fresh, newPassword = secrets[^1] }, Json);
        errorBodies.Append(await repeat.Content.ReadAsStringAsync());

        var logs = sink.Everything();
        Assert.False(string.IsNullOrWhiteSpace(logs), "the sweep captured no log output, so it proves nothing");
        Assert.Contains("password", errorBodies.ToString(), StringComparison.OrdinalIgnoreCase); // rejections explain themselves, without echoing values

        foreach (var secret in secrets.Distinct())
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, errorBodies.ToString(), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("$argon2", logs + errorBodies, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public string Everything() => string.Join('\n', _lines);

        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, _lines);

        public void Dispose()
        {
        }

        private sealed class Capture(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var text = new StringBuilder($"{category} {formatter(state, exception)}");
                if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                {
                    foreach (var (key, value) in pairs)
                    {
                        text.Append(' ').Append(key).Append('=').Append(value);
                    }
                }

                if (exception is not null)
                {
                    text.Append(' ').Append(exception);
                }

                lines.Enqueue(text.ToString());
            }
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
