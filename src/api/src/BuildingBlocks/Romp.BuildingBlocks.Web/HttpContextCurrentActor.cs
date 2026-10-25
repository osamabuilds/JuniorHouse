using Microsoft.AspNetCore.Http;
using Romp.BuildingBlocks.Persistence.Auditing;

namespace Romp.BuildingBlocks.Web;

/// <summary>
/// Attributes changes to the signed-in user. An <see cref="ActorScope"/> wins (operator verbs),
/// and anything without a user (background work, anonymous requests) is <c>system</c>. A name
/// outside the audit charset is reported as <c>unknown</c> rather than written to INSR_BY.
/// </summary>
public sealed class HttpContextCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private const string System = "system";
    private const string Unknown = "unknown";

    public string UserName
    {
        get
        {
            if (ActorScope.Current is { } scoped)
            {
                return scoped;
            }

            var identity = accessor.HttpContext?.User.Identity;
            if (identity is not { IsAuthenticated: true })
            {
                return System;
            }

            return ActorScope.IsValid(identity.Name) ? identity.Name! : Unknown;
        }
    }
}
