using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Sessions;

/// <summary>Sessions task 15 (AC-8, observability AC-9): failed sign-ins, throttling and refresh reuse raise stable security events with no secret in them.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class SessionSecurityEventTests(IdentityPostgres postgres)
{
    private const string Category = "Romp.Security";
    private const string Typed = "typed-password-canary-9981";

    private sealed record Captured(string Category, int EventId, LogLevel Level, string Text);

    private sealed class CapturingProvider : ILoggerProvider
    {
        public ConcurrentQueue<Captured> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class Sink(string category, ConcurrentQueue<Captured> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // The rendered message plus every structured value, so a secret cannot hide in a parameter the text leaves out.
                var structured = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(' ', pairs.Select(p => $"{p.Key}={p.Value}"))
                    : string.Empty;
                entries.Enqueue(new Captured(category, eventId.Id, logLevel, formatter(state, exception) + " " + structured));
            }
        }
    }

    private IdentityHost Host(CapturingProvider provider) => new(postgres, configure: services =>
        services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(provider))));

    private static List<Captured> Security(CapturingProvider provider) =>
        provider.Entries.Where(e => e.Category == Category).ToList();

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task EmitsStableEventIds_WithoutSecrets()
    {
        var provider = new CapturingProvider();
        using var host = Host(provider);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        // 1001: an unknown name, a wrong password.
        await host.SendAsync(new SignInCommand("nobody.at.all", new SecretString(Typed), "203.0.113.30"));
        await host.SendAsync(new SignInCommand(created.UserName, new SecretString(Typed), "203.0.113.31"));

        var failures = Security(provider).Where(e => e.EventId == 1001).ToList();
        Assert.Equal(2, failures.Count);
        Assert.All(failures, f => Assert.Equal(LogLevel.Warning, f.Level));
        Assert.Contains(failures, f => f.Text.Contains("bad_credentials", StringComparison.Ordinal));

        // 1004: the third failure from one address starts a delay, and the next attempt is throttled.
        for (var i = 0; i < 3; i++)
        {
            await host.SendAsync(new SignInCommand(created.UserName, new SecretString(Typed), "203.0.113.32"));
        }

        await host.SendAsync(new SignInCommand(created.UserName, new SecretString(Typed), "203.0.113.32"));
        var throttled = Security(provider).Where(e => e.EventId == 1004).ToList();
        Assert.NotEmpty(throttled);
        Assert.Contains(throttled, t => t.Text.Contains("signin_throttled", StringComparison.Ordinal));

        // 1003: a rotated refresh value presented again after the grace.
        var issued = await host.SendAsync(new IssueSessionCommand(created.UserId, null, "203.0.113.33", "agent"));
        host.Time.Now += TimeSpan.FromMinutes(1);
        await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));
        host.Time.Now += TimeSpan.FromSeconds(30);
        await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        var reuse = Assert.Single(Security(provider), e => e.EventId == 1003);
        Assert.Equal(LogLevel.Warning, reuse.Level);
        Assert.Contains("refresh_reuse", reuse.Text, StringComparison.Ordinal);
        Assert.Contains(issued.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture), reuse.Text, StringComparison.Ordinal);

        // No secret anywhere in any captured entry (all categories).
        var everything = string.Join('\n', provider.Entries.Select(e => e.Text));
        Assert.DoesNotContain(Typed, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(created.TemporaryPassword.Reveal(), everything, StringComparison.Ordinal);
        Assert.DoesNotContain(issued.Tokens.Access.Reveal(), everything, StringComparison.Ordinal);
        Assert.DoesNotContain(issued.Tokens.Refresh.Reveal(), everything, StringComparison.Ordinal);
        Assert.DoesNotContain("$argon2", everything, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task ASuccessfulSignIn_AndARefreshInsideTheGrace_RaiseNoSecurityEvent()
    {
        var provider = new CapturingProvider();
        using var host = Host(provider);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        await host.SendAsync(new SignInCommand(created.UserName, created.TemporaryPassword, "203.0.113.34"));
        var issued = await host.SendAsync(new IssueSessionCommand(created.UserId, null, "203.0.113.34", null));
        host.Time.Now += TimeSpan.FromMinutes(1);
        await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));
        host.Time.Now += TimeSpan.FromSeconds(3);
        await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal())); // a concurrent retry, not theft

        Assert.Empty(Security(provider));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task AnInactiveAccount_AndASpentTemporaryPassword_AreLoggedWithTheirOwnOutcome_ButReturnTheSameAnswer()
    {
        var provider = new CapturingProvider();
        using var host = Host(provider);
        var inactive = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new DeactivateStaffCommand(inactive.UserId));
        var spent = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new SignInCommand(spent.UserName, spent.TemporaryPassword, "203.0.113.35"));

        var a = await host.SendAsync(new SignInCommand(inactive.UserName, inactive.TemporaryPassword, "203.0.113.36"));
        var b = await host.SendAsync(new SignInCommand(spent.UserName, spent.TemporaryPassword, "203.0.113.37"));

        Assert.Equal(a, b); // the caller cannot tell them apart (AC-7)
        var outcomes = Security(provider).Where(e => e.EventId == 1001).Select(e => e.Text).ToList();
        Assert.Contains(outcomes, t => t.Contains("inactive", StringComparison.Ordinal));
        Assert.Contains(outcomes, t => t.Contains("temporary_expired", StringComparison.Ordinal));
    }
}
