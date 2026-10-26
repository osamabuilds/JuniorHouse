using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Infrastructure.Roles;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Infrastructure.Sessions;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Tests.Roles;

/// <summary>Roles task R2 (AC-3, AC-5): the handler that decides a permission requirement from the database on every request.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class PermissionHandlerTests(IdentityPostgres postgres)
{
    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, int EventId, string Text)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class Sink(string category, ConcurrentQueue<(string, int, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue((category, eventId.Id, formatter(state, exception)));
        }
    }

    private static ClaimsPrincipal Principal(long userId, SessionScope scope = SessionScope.Full, string scheme = SessionPrincipal.Scheme) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(SessionPrincipal.ScopeClaim, scope.ToString()),
            ],
            scheme));

    private static DefaultHttpContext Request(IServiceProvider services, ClaimsPrincipal user) =>
        new DefaultHttpContext { RequestServices = services, User = user };

    private static async Task<bool> AllowsAsync(IdentityPermissionHandler handler, HttpContext http, PermissionCode permission)
    {
        var context = new AuthorizationHandlerContext([new PermissionRequirement(permission)], http.User, http);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static async Task<long> UserWithAsync(IdentityHost host, string role) =>
        (await host.SendAsync(new CreateStaffCommand(IdentityHost.NewUserName(), "Role Person", [await host.RoleIdAsync(role)]))).UserId;

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task RoleChange_AppliesOnNextRequest()
    {
        using var host = new IdentityHost(postgres);
        var handler = new IdentityPermissionHandler();
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        await using var scope = host.Scope();

        var firstRequest = Request(scope.ServiceProvider, Principal(user));
        Assert.True(await AllowsAsync(handler, firstRequest, Permissions.CatalogWrite));
        Assert.False(await AllowsAsync(handler, firstRequest, Permissions.InventoryWrite));

        // The role is taken away between two requests.
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IDNT\".\"USR_ROLE_MAP\" WHERE \"USR_ID\" = {0}", user);

        Assert.True(await AllowsAsync(handler, firstRequest, Permissions.CatalogWrite)); // the same request keeps what it was told
        Assert.False(await AllowsAsync(handler, Request(scope.ServiceProvider, Principal(user)), Permissions.CatalogWrite)); // the next one does not
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task ADeactivatedAccount_LosesEverythingOnTheNextRequest()
    {
        using var host = new IdentityHost(postgres);
        var handler = new IdentityPermissionHandler();
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        await using var scope = host.Scope();
        Assert.True(await AllowsAsync(handler, Request(scope.ServiceProvider, Principal(user)), Permissions.CatalogRead));

        await host.SendAsync(new DeactivateStaffCommand(user));

        Assert.False(await AllowsAsync(handler, Request(scope.ServiceProvider, Principal(user)), Permissions.CatalogRead));
    }

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData(SessionScope.EnrolMfa)]
    [InlineData(SessionScope.ChangePassword)]
    public async Task LimitedScope_OnlyAllowsEnrolmentAndPassword(SessionScope scope)
    {
        using var host = new IdentityHost(postgres);
        var handler = new IdentityPermissionHandler();
        var owner = await UserWithAsync(host, RoleCodes.Owner);
        await using var services = host.Scope();

        // Not even the Owner can use a permission with a session that still owes a step; the enrolment and password routes ask only for
        // an authenticated caller, so they are the only ones such a session can reach.
        foreach (var permission in Permissions.All)
        {
            Assert.False(await AllowsAsync(handler, Request(services.ServiceProvider, Principal(owner, scope)), permission), permission.Value);
        }
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task OnlyASessionPrincipal_IsDecidedHere_TheDevelopmentUserIsLeftToItsOwnHandler()
    {
        using var host = new IdentityHost(postgres);
        var handler = new IdentityPermissionHandler();
        await using var services = host.Scope();
        var dev = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "dev-staff")], RompAuthorizationExtensions.DevelopmentScheme));
        var context = new AuthorizationHandlerContext([new PermissionRequirement(Permissions.OpsAdmin)], dev, Request(services.ServiceProvider, dev));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed); // neither allowed nor refused: another handler may decide
    }

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    [InlineData("0")]
    public async Task AMissingOrMalformedUserId_AllowsNothing(string? id)
    {
        using var host = new IdentityHost(postgres);
        var handler = new IdentityPermissionHandler();
        await using var services = host.Scope();
        var claims = new List<Claim> { new(SessionPrincipal.ScopeClaim, nameof(SessionScope.Full)) };
        if (id is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SessionPrincipal.Scheme));

        Assert.False(await AllowsAsync(handler, Request(services.ServiceProvider, principal), Permissions.CatalogRead));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task SeveralRequirements_OnOneRequest_CostOneQuery()
    {
        var counter = new CommandCounter();
        using var host = new IdentityHost(postgres, counter);
        var handler = new IdentityPermissionHandler();
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        await using var services = host.Scope();
        var http = Request(services.ServiceProvider, Principal(user));
        counter.Reset();

        await AllowsAsync(handler, http, Permissions.CatalogRead); // the group's permission
        await AllowsAsync(handler, http, Permissions.CatalogWrite); // the route's permission
        await AllowsAsync(handler, http, Permissions.InventoryWrite);

        Assert.Equal(1, counter.Count);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task ARefusal_IsLoggedWithThePermission_ButTheHandlerGivesNoReasonToShow()
    {
        var capture = new Capture();
        using var host = new IdentityHost(postgres, configure: services =>
            services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(capture))));
        var handler = new IdentityPermissionHandler();
        var user = await UserWithAsync(host, RoleCodes.Support);
        await using var services = host.Scope();
        var http = Request(services.ServiceProvider, Principal(user));
        var context = new AuthorizationHandlerContext([new PermissionRequirement(Permissions.CatalogWrite)], http.User, http);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(context.FailureReasons); // nothing that could be echoed into a response
        var entry = Assert.Single(capture.Entries, e => e.Category == "Romp.Security");
        Assert.Equal(1005, entry.EventId);
        Assert.Contains("catalog.write", entry.Text, StringComparison.Ordinal);
        Assert.Contains(user.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task AGrantedRequest_RaisesNoSecurityEvent()
    {
        var capture = new Capture();
        using var host = new IdentityHost(postgres, configure: services =>
            services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(capture))));
        var user = await UserWithAsync(host, RoleCodes.Catalogue);
        await using var services = host.Scope();

        await AllowsAsync(new IdentityPermissionHandler(), Request(services.ServiceProvider, Principal(user)), Permissions.CatalogWrite);

        Assert.DoesNotContain(capture.Entries, e => e.Category == "Romp.Security");
    }
}
