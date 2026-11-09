using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Mfa;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// MFA task 11 (AC-7): the authenticator secret, the address that carries it, the recovery codes and the typed codes must not appear in
/// any log line (message, structured values, exception text) or in any error response. The secret and the recovery codes are allowed in
/// exactly the responses that hand them to the person (enrolment start, confirm, regenerate), so those bodies are left out of the
/// sweep. The sweep also proves it captured something, so it cannot pass by being empty.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class MfaSecretLeakTests(ProductionApiFixture api)
{
    private const string Own = "Correct-Horse-Battery-Staple-42";

    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task NothingSensitive_AppearsInOutput()
    {
        var sink = new LeakCapture();
        using var factory = api.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:UseRealAuthentication", "true");
            builder.UseSetting("Identity:AllowInsecureCookies", "true");
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(sink));
        });
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        string name;
        string temporary;
        using (var scope = api.Services.CreateScope())
        {
            name = $"leak.{Guid.NewGuid():N}"[..22];
            var created = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateStaffCommand(name, "Leak Check", [2]));
            temporary = created.TemporaryPassword.Reveal();
        }

        var errorBodies = new StringBuilder();
        string Csrf() => JsonSerializer.Deserialize<JsonElement>(browser.GetStringAsync("/api/auth/me").GetAwaiter().GetResult(), Json).GetProperty("csrfToken").GetString()!;
        HttpRequestMessage Post(string path, object? body = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
            request.Headers.Add("X-Romp-Csrf", Csrf());
            return request;
        }

        await browser.PostAsJsonAsync("/api/auth/sign-in", new { userName = name, password = temporary }, Json);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(Post("/api/auth/password", new { currentPassword = temporary, newPassword = Own }))).StatusCode);

        // Enrol: the secret and the recovery codes are shown to the person here, and only here.
        var started = await browser.SendAsync(Post("/api/auth/mfa/enrol/start"));
        var startedJson = JsonSerializer.Deserialize<JsonElement>(await started.Content.ReadAsStringAsync(), Json);
        var secretText = startedJson.GetProperty("secretText").GetString()!;
        var uri = startedJson.GetProperty("otpAuthUri").GetString()!;
        var secret = Base32.Decode(secretText);
        var secrets = new List<string> { secretText, secretText.Replace(" ", string.Empty, StringComparison.Ordinal), Base32.Encode(secret), uri, Uri.EscapeDataString(uri) };

        // Wrong and malformed codes during enrolment: the failures are where secrets leak.
        foreach (var bad in new[] { "000000", "12ab", string.Empty })
        {
            errorBodies.Append(await (await browser.SendAsync(Post("/api/auth/mfa/enrol/confirm", new { code = bad }))).Content.ReadAsStringAsync());
        }

        var confirmed = await browser.SendAsync(Post("/api/auth/mfa/enrol/confirm", new { code = Totp.Code(secret, Totp.StepOf(DateTimeOffset.UtcNow)) }));
        var codes = JsonSerializer.Deserialize<JsonElement>(await confirmed.Content.ReadAsStringAsync(), Json).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList();
        foreach (var code in codes)
        {
            secrets.Add(code);
            secrets.Add(code.Replace("-", string.Empty, StringComparison.Ordinal));
        }

        // Starting again once enrolled, a replayed code, a wrong code at the second step, and regenerating with a bad code.
        errorBodies.Append(await (await browser.SendAsync(Post("/api/auth/mfa/enrol/start"))).Content.ReadAsStringAsync());
        errorBodies.Append(await (await browser.SendAsync(Post("/api/auth/mfa/recovery-codes", new { code = "000000" }))).Content.ReadAsStringAsync());
        await browser.PostAsync("/api/auth/sign-out", null);
        await browser.PostAsJsonAsync("/api/auth/sign-in", new { userName = name, password = Own }, Json);
        // Two failures then a success per sign-in (the delay starts at the third failure in a row).
        foreach (var bad in new[] { Totp.Code(secret, Totp.StepOf(DateTimeOffset.UtcNow)), "999999" })
        {
            errorBodies.Append(await (await browser.PostAsJsonAsync("/api/auth/mfa/verify", new { code = bad }, Json)).Content.ReadAsStringAsync());
        }

        var verified = await browser.PostAsJsonAsync("/api/auth/mfa/verify", new { code = codes[1] }, Json); // a recovery code works
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        errorBodies.Append(await verified.Content.ReadAsStringAsync());

        await browser.PostAsync("/api/auth/sign-out", null);
        await browser.PostAsJsonAsync("/api/auth/sign-in", new { userName = name, password = Own }, Json);
        foreach (var bad in new[] { codes[0].ToLowerInvariant() + "x", "not-a-code" })
        {
            errorBodies.Append(await (await browser.PostAsJsonAsync("/api/auth/mfa/verify", new { code = bad }, Json)).Content.ReadAsStringAsync());
        }

        var logs = sink.Everything();
        Assert.False(string.IsNullOrWhiteSpace(logs), "the sweep captured no log output, so it proves nothing");
        Assert.Contains("Romp.Security", logs, StringComparison.Ordinal); // the failures did raise security events: proof the paths ran
        foreach (var value in secrets.Distinct())
        {
            Assert.DoesNotContain(value, logs, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(value, errorBodies.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        // A six-digit number can appear by chance (a port, a duration), so what is checked is a typed code under a "code" key.
        Assert.DoesNotMatch(new Regex(@"(?i)code[""']?\s*[:=]\s*[""']?\d{6}\b"), logs);
        Assert.DoesNotContain(Own, logs + errorBodies, StringComparison.Ordinal);
        Assert.DoesNotContain(temporary, logs + errorBodies, StringComparison.Ordinal);
        Assert.DoesNotContain("$argon2", logs + errorBodies, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LeakCapture : ILoggerProvider
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
}
