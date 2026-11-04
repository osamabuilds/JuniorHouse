using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S5 (ACT-11, ACT-12): refresh with rotation, reuse detection and sign-out.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class RefreshTests(CustomersPostgres postgres)
{
    private static async Task<StartedSession> SignedInAsync(AccountsHarness harness)
    {
        var result = await harness.Send(RegisterTests.Register());
        return result.Session!;
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task Refresh_ExchangesTheRefreshValueForANewPair()
    {
        using var harness = new AccountsHarness(postgres);
        var session = await SignedInAsync(harness);

        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        var result = await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.Rotated, result.Outcome);
        Assert.NotEqual(session.Tokens.Access.Reveal(), result.Session!.Tokens.Access.Reveal());
        Assert.NotEqual(session.Tokens.Refresh.Reveal(), result.Session.Tokens.Refresh.Reveal());
        Assert.True(result.Session.AccessExpiresAt > session.AccessExpiresAt);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task Refresh_AnOldValueUsedAgainAfterTheGrace_RevokesTheWholeSession()
    {
        using var harness = new AccountsHarness(postgres);
        var session = await SignedInAsync(harness);
        var rotated = await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()));
        harness.Clock.Advance(CustomerSession.RefreshGrace + TimeSpan.FromSeconds(1));

        var reuse = await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.ReuseDetected, reuse.Outcome);
        Assert.Null(reuse.Session);
        var stored = await harness.Read(db => db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.SessionId));
        Assert.Equal(CustomerSessionRevokeReason.RefreshReuse, stored.RevokeReason);
        var after = await harness.Send(new RefreshSessionCommand(rotated.Session!.Tokens.Refresh.Reveal()));
        Assert.Equal(RefreshOutcome.Unusable, after.Outcome);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task Refresh_TheOldValueWithinTheGrace_IsAConcurrentRetry_NotTheft()
    {
        using var harness = new AccountsHarness(postgres);
        var session = await SignedInAsync(harness);
        await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()));

        var second = await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.ConcurrentRefresh, second.Outcome);
        var stored = await harness.Read(db => db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.SessionId));
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task Refresh_AnUnknownValue_IsUnusable()
    {
        using var harness = new AccountsHarness(postgres);

        var result = await harness.Send(new RefreshSessionCommand("not-a-real-refresh-value"));

        Assert.Equal(RefreshOutcome.Unusable, result.Outcome);
        Assert.Null(result.Session);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task Refresh_AfterTheIdleWindow_IsUnusable()
    {
        using var harness = new AccountsHarness(postgres);
        var session = await SignedInAsync(harness);

        harness.Clock.Advance(TimeSpan.FromMinutes(31));
        var result = await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.Unusable, result.Outcome);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task SignOut_RevokesTheSession_SoItsValuesStopWorking()
    {
        using var harness = new AccountsHarness(postgres);
        var session = await SignedInAsync(harness);

        await harness.Send(new SignOutCommand(session.Tokens.Access.Reveal()));

        var stored = await harness.Read(db => db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.SessionId));
        Assert.Equal(CustomerSessionRevokeReason.SignOut, stored.RevokeReason);
        Assert.Equal(RefreshOutcome.Unusable, (await harness.Send(new RefreshSessionCommand(session.Tokens.Refresh.Reveal()))).Outcome);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task SignOut_AnUnknownOrEmptyValue_ChangesNothingAndDoesNotFail()
    {
        using var harness = new AccountsHarness(postgres);

        await harness.Send(new SignOutCommand("unknown-value"));
        await harness.Send(new SignOutCommand(string.Empty));
    }
}
