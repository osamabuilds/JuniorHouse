using System.Globalization;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.StaffAccounts;

/// <summary>Body of POST /api/identity/staff. Role ids are checked against the role lookup by the handler.</summary>
public sealed record CreateStaffRequest(string UserName, string DisplayName, IReadOnlyList<short> RoleIds);

/// <summary>Body of POST /api/auth/password. Plain strings on the wire; they become <see cref="SecretString"/> at the edge.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// The one response that carries a generated password (AC-4). The only place a <see cref="SecretString"/> is revealed, into a
/// response marked <c>no-store</c>.
/// </summary>
public sealed record TemporaryPasswordResponse(long UserId, string UserName, string TemporaryPassword, DateTimeOffset ExpiresAt);

// The session the caller signed in with. The sessions feature sets these claims; until it lands only tests do.
internal static class SessionClaims
{
    public const string SessionId = "sid";

    public static long? UserId(ClaimsPrincipal user) => ReadLong(user.FindFirstValue(ClaimTypes.NameIdentifier));

    public static long? Session(ClaimsPrincipal user) => ReadLong(user.FindFirstValue(SessionId));

    private static long? ReadLong(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}

internal static class StaffEndpoints
{
    private const long MaxBodyBytes = 8 * 1024;

    public static void MapStaffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var staff = endpoints.MapSecuredGroup("/api/identity/staff", Permissions.StaffManage).WithTags("Identity")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CsrfFilter>();

        staff.MapGet("", async (ISender sender, CancellationToken cancellationToken, string? search = null, bool? isActive = null,
                int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListStaffQuery(search, isActive, page, pageSize), cancellationToken)));

        staff.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetStaffQuery(id), cancellationToken)));

        staff.MapPost("", async (CreateStaffRequest body, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new CreateStaffCommand(body.UserName, body.DisplayName, body.RoleIds ?? []), cancellationToken);
                return OneTimePassword(http, result, created: true);
            })
            .RequireRateLimiting(RateLimitPolicies.Strict);

        staff.MapPost("/{id:long}/deactivate", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeactivateStaffCommand(id), cancellationToken);
            return Results.NoContent();
        });

        staff.MapPost("/{id:long}/reactivate", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ReactivateStaffCommand(id), cancellationToken);
            return Results.NoContent();
        });

        staff.MapPost("/{id:long}/reset-password", async (long id, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
                OneTimePassword(http, await sender.Send(new ResetPasswordCommand(id), cancellationToken), created: false))
            .RequireRateLimiting(RateLimitPolicies.Strict);

        // An administrator clears another person's second factor (mfa AC-8). The command refuses the caller's own account.
        staff.MapPost("/{id:long}/reset-mfa", async (long id, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
            {
                await sender.Send(new Romp.Modules.Identity.Application.Mfa.ResetStaffMfaCommand(id, SessionClaims.UserId(http.User) ?? 0), cancellationToken);
                return Results.NoContent();
            })
            .RequireRateLimiting(RateLimitPolicies.Strict);

        // Any signed-in person may change their own password; the account is the session's, never one named in the body (SEC-05).
        endpoints.MapSecuredGroup("/api/auth").WithTags("Identity")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CsrfFilter>()
            .MapPost("/password", async (ChangePasswordRequest body, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
            {
                if (SessionClaims.UserId(http.User) is not { } userId)
                {
                    return Results.Unauthorized();
                }

                await sender.Send(
                    new ChangeOwnPasswordCommand(userId, SessionClaims.Session(http.User), new SecretString(body.CurrentPassword ?? string.Empty), new SecretString(body.NewPassword ?? string.Empty)),
                    cancellationToken);
                return Results.NoContent();
            })
            .RequireRateLimiting(RateLimitPolicies.Strict);
    }

    private static IResult OneTimePassword(HttpContext http, TemporaryPasswordResult result, bool created)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        var body = new TemporaryPasswordResponse(result.UserId, result.UserName, result.TemporaryPassword.Reveal(), result.ExpiresAt);
        return created ? Results.Created(new Uri($"/api/identity/staff/{result.UserId}", UriKind.Relative), body) : Results.Ok(body);
    }
}
