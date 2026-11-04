using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S6 (ACT-14): the hook the cart and the wishlist use to merge a guest basket after sign-in.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class SignedInHookTests(CustomersPostgres postgres)
{
    private readonly string _ip = $"198.19.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task SignIn_TellsTheHookWhoSignedInAndWhichGuestSessionTheyBrought()
    {
        using var harness = new AccountsHarness(postgres);
        var email = $"hook.{Guid.NewGuid():N}@example.test";
        var registered = await harness.Send(RegisterTests.Register(email));
        harness.Clock.Advance(TimeSpan.FromMinutes(1));

        await harness.Send(new SignInCommand(email, new SecretString("Strong1Pass"), false, null, _ip, null, null, "guest-abc"));

        var call = harness.Hook.Calls[^1];
        Assert.Equal(registered.Session!.CustomerId, call.CustomerId);
        Assert.Equal("guest-abc", call.GuestSessionId);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task Register_TellsTheHookToo()
    {
        using var harness = new AccountsHarness(postgres);

        var registered = await harness.Send(RegisterTests.Register() with { GuestSessionId = "guest-xyz" });

        var call = Assert.Single(harness.Hook.Calls);
        Assert.Equal(registered.Session!.CustomerId, call.CustomerId);
        Assert.Equal("guest-xyz", call.GuestSessionId);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task AFailingHook_NeverStopsTheSignIn()
    {
        using var harness = new AccountsHarness(postgres);
        var email = $"hookfail.{Guid.NewGuid():N}@example.test";
        await harness.Send(RegisterTests.Register(email));
        harness.Hook.Fail = true;
        harness.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await harness.Send(new SignInCommand(email, new SecretString("Strong1Pass"), false, null, _ip, null, null));

        Assert.Equal(SignInStatus.SignedIn, result.Status);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task AFailedSignIn_DoesNotCallTheHook()
    {
        using var harness = new AccountsHarness(postgres);
        var email = $"hookbad.{Guid.NewGuid():N}@example.test";
        await harness.Send(RegisterTests.Register(email));
        var before = harness.Hook.Calls.Count;

        await harness.Send(new SignInCommand(email, new SecretString("Wrong1Pass"), false, null, _ip, null, null));

        Assert.Equal(before, harness.Hook.Calls.Count);
    }
}
