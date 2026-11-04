using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

/// <summary>The operator's bootstrap and break-glass commands (staff-accounts step 10). The collection runs one test at a time, so a test may empty the Owner role.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class OwnerCommandsTests(IdentityPostgres postgres)
{
    private const string GoodPassword = "owner-bootstrap-passphrase-77";
    private const string OperatorActor = "operator-cli";

    private static async Task NoOwnersExistAsync(IdentityHost host)
    {
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        var owner = await host.RoleIdAsync(RoleCodes.Owner);
        await db.Set<StaffUserRole>().Where(r => r.RoleId == owner).ExecuteDeleteAsync();
    }

    private static async Task<long> OwnerExistsAsync(IdentityHost host)
    {
        await NoOwnersExistAsync(host);
        using (ActorScope.Begin(OperatorActor))
        {
            return await host.SendAsync(new CreateOwnerCommand(IdentityHost.NewUserName(), "The Owner", new SecretString(GoodPassword)));
        }
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task CreateOwner_RefusesASecondOwner()
    {
        using var host = new IdentityHost(postgres);
        await NoOwnersExistAsync(host);

        var id = await host.SendAsync(new CreateOwnerCommand(IdentityHost.NewUserName(), "The Owner", new SecretString(GoodPassword)));

        var stored = await host.LoadAsync(id);
        var ownerRole = await host.RoleIdAsync(RoleCodes.Owner);
        Assert.True(stored.IsActive);
        Assert.True(stored.MustChangePassword);
        Assert.Equal([ownerRole], stored.RoleIds);
        Assert.True(StaffHandlerHelpers.Hasher.Verify(new SecretString(GoodPassword), stored.PasswordHash));

        var second = IdentityHost.NewUserName();
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CreateOwnerCommand(second, "Another", new SecretString(GoodPassword))));
        await using var check = host.Scope();
        Assert.False(await check.ServiceProvider.GetRequiredService<IdntDbContext>().Staff.AnyAsync(u => u.UserName == second));
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task CreateOwner_RefusesAWeakPassword_AndChangesNothing()
    {
        using var host = new IdentityHost(postgres);
        await NoOwnersExistAsync(host);
        var name = IdentityHost.NewUserName();

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new CreateOwnerCommand(name, "The Owner", new SecretString("short"))));

        await using var check = host.Scope();
        Assert.False(await check.ServiceProvider.GetRequiredService<IdntDbContext>().Staff.AnyAsync(u => u.UserName == name));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task CreateOwner_IsAttributedToTheOperator()
    {
        using var host = new IdentityHost(postgres);

        var id = await OwnerExistsAsync(host);

        Assert.Equal(OperatorActor, (await host.LoadAsync(id)).InsrBy);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task ResetOwner_ClearsPasswordAndMfa_AndAudits()
    {
        using var host = new IdentityHost(postgres);
        var id = await OwnerExistsAsync(host);
        var owner = await host.LoadAsync(id);
        const string Replacement = "break-glass-replacement-pass-12";

        using (ActorScope.Begin(OperatorActor))
        {
            await host.SendAsync(new ResetOwnerCommand(owner.UserName, new SecretString(Replacement), ClearMfa: true));
        }

        var stored = await host.LoadAsync(id);
        Assert.True(StaffHandlerHelpers.Hasher.Verify(new SecretString(Replacement), stored.PasswordHash));
        Assert.False(StaffHandlerHelpers.Hasher.Verify(new SecretString(GoodPassword), stored.PasswordHash));
        Assert.True(stored.MustChangePassword);
        Assert.Contains((id, (long?)null), host.Sessions.Calls);
        Assert.Contains(id, host.MfaReset.Cleared);
        Assert.Equal(OperatorActor, stored.UpdtBy);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task ResetOwner_WithOnlyMfa_KeepsThePassword()
    {
        using var host = new IdentityHost(postgres);
        var id = await OwnerExistsAsync(host);
        var before = (await host.LoadAsync(id)).PasswordHash;

        await host.SendAsync(new ResetOwnerCommand((await host.LoadAsync(id)).UserName, null, ClearMfa: true));

        Assert.Equal(before, (await host.LoadAsync(id)).PasswordHash);
        Assert.Contains(id, host.MfaReset.Cleared);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task ResetOwner_RefusesNonOwners_UnknownNames_AndAnEmptyRequest()
    {
        using var host = new IdentityHost(postgres);
        var catalogue = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ResetOwnerCommand(catalogue.UserName, new SecretString(GoodPassword), false)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new ResetOwnerCommand("nobody.by.that.name", new SecretString(GoodPassword), false)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ResetOwnerCommand(catalogue.UserName, null, false)));
        Assert.Empty(host.MfaReset.Cleared);
    }
}
