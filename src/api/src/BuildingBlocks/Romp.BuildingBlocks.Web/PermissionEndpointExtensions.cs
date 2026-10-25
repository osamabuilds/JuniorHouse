using Microsoft.AspNetCore.Builder;

namespace Romp.BuildingBlocks.Web;

public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Requires an authenticated caller who holds <paramref name="permission"/>. Group-level and route-level declarations both apply, so
    /// a mutating route in a read group adds the write permission and needs both.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, PermissionCode permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
        builder.WithMetadata(new PermissionMetadata(permission));
        return builder;
    }
}
