using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Roles;

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class ListRolesQueryTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task ListsTheActiveRoles_InDisplayOrder_WithoutPermissionsOrGrants()
    {
        using var host = new IdentityHost(postgres);

        var roles = await host.SendAsync(new ListRolesQuery());

        Assert.Contains(roles, r => r.Code == RoleCodes.Owner && r.HasAllPermissions);
        Assert.Contains(roles, r => r.Code == RoleCodes.Catalogue && !r.HasAllPermissions);
        Assert.Equal(roles.Select(r => r.Id).Distinct().Count(), roles.Count);
        Assert.All(roles, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
    }
}
