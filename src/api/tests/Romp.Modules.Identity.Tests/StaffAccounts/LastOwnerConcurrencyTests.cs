using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class LastOwnerConcurrencyTests(IdentityPostgres postgres)
{
    private const int Rounds = 15;

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task TwoParallelDeactivations_LeaveOneActiveOwner()
    {
        using var host = new IdentityHost(postgres);
        var owner = await host.RoleIdAsync(RoleCodes.Owner);

        for (var round = 0; round < Rounds; round++)
        {
            await using (var scope = host.Scope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
                await db.Staff.Where(u => u.Roles.Any(r => r.RoleId == owner)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
            }

            var a = await StaffHandlerHelpers.CreateAsync(host, owner);
            var b = await StaffHandlerHelpers.CreateAsync(host, owner);

            var outcomes = await Task.WhenAll(
                Try(host, new DeactivateStaffCommand(a.UserId)),
                Try(host, new DeactivateStaffCommand(b.UserId)));

            Assert.Equal(1, outcomes.Count(ok => ok));
            await using var check = host.Scope();
            var active = await check.ServiceProvider.GetRequiredService<IdntDbContext>().Staff
                .CountAsync(u => u.IsActive && u.Roles.Any(r => r.RoleId == owner));
            Assert.Equal(1, active);
        }
    }

    private static async Task<bool> Try(IdentityHost host, DeactivateStaffCommand command)
    {
        try
        {
            await host.SendAsync(command);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
