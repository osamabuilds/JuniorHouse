using System.Globalization;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Infrastructure.Sessions;

namespace Romp.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Decides a <see cref="PermissionRequirement"/> from the database on every request (roles AC-3, SEC-08). The cookie carries no
/// permissions, so a role change or a deactivation applies on the next request. A person's permissions are read once per request and
/// kept in <c>HttpContext.Items</c> only, so a route that asks for a group permission and a route permission costs one query, and
/// nothing outlives the request.
/// </summary>
/// <remarks>
/// Only principals of the session scheme are decided here; the development stand-in user is left to its own handler. A session that
/// still owes a step (scope other than Full) is allowed no permission at all: the enrolment and password routes ask only for an
/// authenticated caller, so they are the only routes such a session reaches. A refusal gives no reason (the 403 never names the
/// permission) and is logged as a security event that does.
/// </remarks>
public sealed class IdentityPermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly object CacheKey = new();

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true, AuthenticationType: SessionPrincipal.Scheme } || context.Resource is not HttpContext http)
        {
            return;
        }

        if (context.User.FindFirstValue(SessionPrincipal.ScopeClaim) != nameof(SessionScope.Full)
            || !long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            || userId <= 0)
        {
            return;
        }

        if (http.Items[CacheKey] is not IReadOnlySet<string> permissions)
        {
            permissions = await http.RequestServices.GetRequiredService<ISender>().Send(new GetEffectivePermissionsQuery(userId), http.RequestAborted);
            http.Items[CacheKey] = permissions;
        }

        if (permissions.Contains(requirement.Code.Value))
        {
            context.Succeed(requirement);
            return;
        }

        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(SecurityEvents.Category).Forbidden(userId, requirement.Code.Value);
    }
}
