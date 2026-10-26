using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;

namespace Romp.Api.IntegrationTests.Identity;

[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class RoleEndpointsTests(ProductionApiFixture api)
{
    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task ListRoles_NeedsStaffManage_AndReturnsTheRoleLookup()
    {
        var response = await api.Client.GetAsync("/api/identity/roles");
        var roles = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(roles.GetArrayLength() >= 5);

        using var denied = api.Factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IAuthorizationHandler>(new DenyAll()))).CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/identity/roles")).StatusCode);
    }

    private sealed class DenyAll : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            context.Fail();
            return Task.CompletedTask;
        }
    }
}
