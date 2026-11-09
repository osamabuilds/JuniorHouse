using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Romp.BuildingBlocks.Web;

public static class SecuredEndpointExtensions
{
    /// <summary>
    /// The only way a module maps an endpoint group (ADR 0011): the group always carries an authorization policy, so an
    /// endpoint cannot be added without deciding who may call it. An architecture test rejects a bare <c>MapGroup</c>.
    /// </summary>
    public static RouteGroupBuilder MapSecuredGroup(this IEndpointRouteBuilder endpoints, string prefix, string policy) =>
        endpoints.MapGroup(prefix).RequireAuthorization(policy);

    /// <summary>
    /// The permission-based form (Sprint 8, SEC-08): the group requires an authenticated caller holding <paramref name="permission"/>,
    /// checked on every request. Declare the read permission here and add the write permission on each mutating route with
    /// <see cref="PermissionEndpointExtensions.RequirePermission{TBuilder}"/>. The policy-name overload above stays until roles task R9.
    /// </summary>
    public static RouteGroupBuilder MapSecuredGroup(this IEndpointRouteBuilder endpoints, string prefix, PermissionCode permission) =>
        endpoints.MapGroup(prefix).RequirePermission(permission);
}
