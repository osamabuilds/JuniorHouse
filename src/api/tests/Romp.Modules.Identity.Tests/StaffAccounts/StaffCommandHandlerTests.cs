using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

internal static class StaffHandlerHelpers
{
    public static readonly Argon2PasswordHasher Hasher =
        new(Options.Create(new Argon2Options { MemoryKiB = 8, Iterations = 1, Parallelism = 1 }));

    public static Task<TemporaryPasswordResult> CreateAsync(IdentityHost host, short roleId, string? name = null) =>
        host.SendAsync(new CreateStaffCommand(name ?? IdentityHost.NewUserName(), "Test Person", [roleId]));
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class CreateStaffHandlerTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-4")]
    [Trait("Spec", "AC-3")]
    public async Task Creates_AnActiveAccount_WithAHashedTemporaryPassword_ShownOnce()
    {
        using var host = new IdentityHost(postgres);
        var role = await host.RoleIdAsync(RoleCodes.Catalogue);

        var result = await StaffHandlerHelpers.CreateAsync(host, role);

        Assert.True(result.TemporaryPassword.Length >= 16);
        Assert.Equal(IdentityHost.Start + StaffUser.TemporaryPasswordLifetime, result.ExpiresAt);
        var stored = await host.LoadAsync(result.UserId);
        Assert.True(stored.IsActive);
        Assert.True(stored.MustChangePassword);
        Assert.True(stored.HasTemporaryPassword);
        Assert.Equal([role], stored.RoleIds);
        Assert.DoesNotContain(result.TemporaryPassword.Reveal(), stored.PasswordHash, StringComparison.Ordinal);
        Assert.True(StaffHandlerHelpers.Hasher.Verify(result.TemporaryPassword, stored.PasswordHash));
        Assert.False(string.IsNullOrWhiteSpace(stored.InsrBy));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task ADuplicateUserName_IgnoringCase_IsRefused()
    {
        using var host = new IdentityHost(postgres);
        var role = await host.RoleIdAsync(RoleCodes.Catalogue);
        var name = IdentityHost.NewUserName();
        await StaffHandlerHelpers.CreateAsync(host, role, name);

        var ex = await Assert.ThrowsAsync<DomainException>(() => StaffHandlerHelpers.CreateAsync(host, role, name.ToUpperInvariant()));

        Assert.Contains("already", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("")]
    public async Task ABadUserName_IsRejectedBeforeAnythingIsStored(string name)
    {
        using var host = new IdentityHost(postgres);
        var role = await host.RoleIdAsync(RoleCodes.Catalogue);

        await Assert.ThrowsAsync<ValidationException>(() => StaffHandlerHelpers.CreateAsync(host, role, name));
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task ARoleThatDoesNotExist_IsRefused()
    {
        using var host = new IdentityHost(postgres);

        await Assert.ThrowsAsync<DomainException>(() => StaffHandlerHelpers.CreateAsync(host, 9999));
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class ResetPasswordHandlerTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-4")]
    [Trait("Spec", "AC-8")]
    public async Task RevokesSessions()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        host.Time.Now = IdentityHost.Start.AddDays(5);

        var reset = await host.SendAsync(new ResetPasswordCommand(created.UserId));

        Assert.NotEqual(created.TemporaryPassword.Reveal(), reset.TemporaryPassword.Reveal());
        Assert.Equal(host.Time.Now + StaffUser.TemporaryPasswordLifetime, reset.ExpiresAt);
        Assert.Equal((created.UserId, (long?)null), Assert.Single(host.Sessions.Calls));
        var stored = await host.LoadAsync(created.UserId);
        Assert.True(stored.MustChangePassword);
        Assert.True(StaffHandlerHelpers.Hasher.Verify(reset.TemporaryPassword, stored.PasswordHash));
        Assert.False(StaffHandlerHelpers.Hasher.Verify(created.TemporaryPassword, stored.PasswordHash));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task ADeactivatedAccount_CannotBeReset_AndNoSessionIsTouched()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new DeactivateStaffCommand(created.UserId));
        host.Sessions.Calls.Clear();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ResetPasswordCommand(created.UserId)));

        Assert.Empty(host.Sessions.Calls);
    }

    [Fact]
    public async Task AnUnknownAccount_IsNotFound()
    {
        using var host = new IdentityHost(postgres);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new ResetPasswordCommand(long.MaxValue)));
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class ChangeOwnPasswordHandlerTests(IdentityPostgres postgres)
{
    private const long SessionId = 77;

    private static SecretString Secret(string value) => new(value);

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task ThePasswordHistory_KeepsTheLastThree_WhenMoreThanThreeAreChosen()
    {
        // Found by the sessions e2e run: the fourth change tried to drop the oldest entry and EF refused ("association severed").
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        var current = created.TemporaryPassword;

        for (var i = 1; i <= 6; i++)
        {
            var next = Secret($"chosen passphrase number {i} {i * 7919}");
            await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, current, next));
            current = next;
        }

        var stored = await host.LoadAsync(created.UserId);
        Assert.Equal(3, stored.PasswordHistory.Count);
        Assert.True(StaffHandlerHelpers.Hasher.Verify(current, stored.PasswordHash));

        // The three kept are the three before the current one: the fourth-last is free to be used again, the third-last is not.
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, current, Secret("chosen passphrase number 3 23757"))));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, current, Secret("chosen passphrase number 2 15838")));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task RequiresCurrentAndRevokesOthers()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(
            new ChangeOwnPasswordCommand(created.UserId, SessionId, Secret("not-the-current-one"), Secret("a brand new passphrase"))));
        Assert.Empty(host.Sessions.Calls);

        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, SessionId, created.TemporaryPassword, Secret("a brand new passphrase")));

        Assert.Equal((created.UserId, (long?)SessionId), Assert.Single(host.Sessions.Calls));
        var stored = await host.LoadAsync(created.UserId);
        Assert.False(stored.MustChangePassword);
        Assert.False(stored.HasTemporaryPassword);
        Assert.True(StaffHandlerHelpers.Hasher.Verify(Secret("a brand new passphrase"), stored.PasswordHash));
        Assert.Single(stored.PasswordHistory);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task ABadNewPassword_IsRefusedWithTheReason_AndNothingChanges()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(
            new ChangeOwnPasswordCommand(created.UserId, SessionId, created.TemporaryPassword, Secret("short"))));

        Assert.Contains(PasswordPolicyMessages.For(PasswordPolicyFailure.TooShort), ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("short", ex.Message.Replace("shorter", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Empty(host.Sessions.Calls);
        Assert.True((await host.LoadAsync(created.UserId)).MustChangePassword);
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task ARecentlyUsedPassword_IsRefused()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, SessionId, created.TemporaryPassword, Secret("first chosen passphrase")));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(
            new ChangeOwnPasswordCommand(created.UserId, SessionId, Secret("first chosen passphrase"), Secret("first chosen passphrase"))));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, SessionId, Secret("first chosen passphrase"), Secret("second chosen passphrase")));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(
            new ChangeOwnPasswordCommand(created.UserId, SessionId, Secret("second chosen passphrase"), Secret("first chosen passphrase"))));
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class DeactivateStaffHandlerTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task Deactivate_RevokesSessions_IsIdempotent_AndReactivateRestores()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        await host.SendAsync(new DeactivateStaffCommand(created.UserId));
        await host.SendAsync(new DeactivateStaffCommand(created.UserId));

        Assert.False((await host.LoadAsync(created.UserId)).IsActive);
        Assert.All(host.Sessions.Calls, c => Assert.Equal((created.UserId, (long?)null), c));
        Assert.NotEmpty(host.Sessions.Calls);

        await host.SendAsync(new ReactivateStaffCommand(created.UserId));
        await host.SendAsync(new ReactivateStaffCommand(created.UserId));
        Assert.True((await host.LoadAsync(created.UserId)).IsActive);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task TheLastActiveOwner_CannotBeDeactivated_ButCanOnceAnotherOwnerExists()
    {
        using var host = new IdentityHost(postgres);
        var owner = await host.RoleIdAsync(RoleCodes.Owner);
        await using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
            await db.Staff.Where(u => u.Roles.Any(r => r.RoleId == owner)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        }

        var first = await StaffHandlerHelpers.CreateAsync(host, owner);
        host.Sessions.Calls.Clear();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DeactivateStaffCommand(first.UserId)));
        Assert.True((await host.LoadAsync(first.UserId)).IsActive);
        Assert.Empty(host.Sessions.Calls);

        var second = await StaffHandlerHelpers.CreateAsync(host, owner);
        await host.SendAsync(new DeactivateStaffCommand(first.UserId));

        Assert.False((await host.LoadAsync(first.UserId)).IsActive);
        Assert.True((await host.LoadAsync(second.UserId)).IsActive);
    }
}
