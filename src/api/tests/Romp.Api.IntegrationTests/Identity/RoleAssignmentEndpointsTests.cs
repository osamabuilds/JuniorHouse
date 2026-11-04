using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Application.StaffAccounts;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>Roles task R3 (AC-6) through the real HTTP API: the role matrix and the replacement of a person's roles.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class RoleAssignmentEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;
    private static readonly int[] TooMany = [1, 2, 3, 4, 5, 6];
    private static readonly int[] NoSuchRole = [9999];
    private static readonly int[] Catalogue = [2];

    private async Task<long> NewUserAsync()
    {
        using var scope = api.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateStaffCommand($"roles.{Guid.NewGuid():N}"[..22], "Role Person", [2]));
        return created.UserId;
    }

    private async Task<short> RoleIdAsync(string code)
    {
        var roles = JsonSerializer.Deserialize<JsonElement>(await api.Client.GetStringAsync("/api/identity/roles"), Json);
        return roles.EnumerateArray().Single(r => r.GetProperty("code").GetString() == code).GetProperty("id").GetInt16();
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task TheMatrix_ListsEveryPermission_AndWhatEachRoleHolds()
    {
        var response = await api.Client.GetAsync("/api/identity/roles/matrix");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var matrix = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);
        Assert.True(matrix.GetProperty("permissions").GetArrayLength() >= 19);
        var orderOps = matrix.GetProperty("roles").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "ORDER_OPS");
        var codes = orderOps.GetProperty("permissionCodes").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains("inventory.read", codes);
        Assert.DoesNotContain("inventory.write", codes);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task PutRoles_ReplacesTheSet()
    {
        var user = await NewUserAsync();
        var support = await RoleIdAsync("SUPPORT");

        var response = await api.Client.PutAsJsonAsync($"/api/identity/staff/{user}/roles", new { roleIds = new[] { support } }, Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var detail = JsonSerializer.Deserialize<JsonElement>(await api.Client.GetStringAsync($"/api/identity/staff/{user}"), Json);
        var roles = detail.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ToList();
        Assert.Equal(["SUPPORT"], roles);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task PutRoles_RefusesBadInput_AndUnknownAccounts()
    {
        var user = await NewUserAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PutAsJsonAsync($"/api/identity/staff/{user}/roles", new { roleIds = TooMany }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PutAsJsonAsync($"/api/identity/staff/{user}/roles", new { roleIds = NoSuchRole }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PutAsJsonAsync("/api/identity/staff/999999999/roles", new { roleIds = Catalogue }, Json)).StatusCode);
        // A body with no list at all is a bad request, not a quiet "remove every role".
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PutAsJsonAsync($"/api/identity/staff/{user}/roles", new { }, Json)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    [Trait("Spec", "AC-5")]
    public async Task BothRoutes_NeedStaffManage()
    {
        var user = await NewUserAsync();
        using var denied = api.Factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IAuthorizationHandler>(new DenyAll()))).CreateClient();

        var put = await denied.PutAsJsonAsync($"/api/identity/staff/{user}/roles", new { roleIds = Catalogue }, Json);
        var matrix = await denied.GetAsync("/api/identity/roles/matrix");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, matrix.StatusCode);
        Assert.DoesNotContain("staff.manage", await put.Content.ReadAsStringAsync(), StringComparison.Ordinal); // the refusal never names the permission
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
