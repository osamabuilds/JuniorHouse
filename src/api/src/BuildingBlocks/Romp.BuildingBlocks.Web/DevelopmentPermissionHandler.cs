using Microsoft.AspNetCore.Authorization;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// Satisfies every permission for the development user (an Owner stand-in, roles spec AC-13). Registered ONLY in Development and
/// Testing by <c>AddRompAuthorization</c>; in any other environment the Identity module's handler decides, and the host refuses to
/// start without a real scheme (<see cref="SecurityGate"/>).
/// </summary>
internal sealed class DevelopmentPermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var identity = context.User.Identity;
        if (identity is { IsAuthenticated: true, AuthenticationType: RompAuthorizationExtensions.DevelopmentScheme })
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
