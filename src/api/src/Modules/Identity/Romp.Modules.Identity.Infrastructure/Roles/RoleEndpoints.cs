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
        var roles = endpoints.MapSecuredGroup("/api/identity/roles", Permissions.StaffManage).WithTags("Identity").AddEndpointFilter<Sessions.CsrfFilter>();

        roles.MapGet("", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListRolesQuery(), cancellationToken)));

        // The read-only matrix: which role holds which permission (roles AC-6).
        roles.MapGet("/matrix", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetRoleMatrixQuery(), cancellationToken)));
    }
}
