using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Domain.Roles;

namespace Romp.Modules.Identity.Infrastructure.Roles;

internal static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSecuredGroup("/api/identity/roles", Permissions.StaffManage).WithTags("Identity").AddEndpointFilter<Sessions.CsrfFilter>()
            .MapGet("", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListRolesQuery(), cancellationToken)));
    }
}
