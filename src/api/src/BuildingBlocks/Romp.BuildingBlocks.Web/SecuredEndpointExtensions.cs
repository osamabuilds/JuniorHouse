using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Romp.BuildingBlocks.Web;

public static class SecuredEndpointExtensions
{
    /// <summary>
    /// The only way a module maps an endpoint group (ADR 0011): the group always carries an authorization policy, so an
    /// endpoint cannot be added without deciding who may call it. An architecture test rejects a bare <c>MapGroup</c>.
    /// </summary>
    public static RouteGroupBuilder MapSecuredGroup(this IEndpointRouteBuilder endpoints, string prefix, string policy = Policies.Staff) =>
        endpoints.MapGroup(prefix).RequireAuthorization(policy);
}
