using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>Body of POST /api/auth/sign-in. Plain strings on the wire; the password becomes a <see cref="SecretString"/> at the edge.</summary>
public sealed record SignInRequest(string? UserName, string? Password);

/// <summary>Answer to a password that was accepted: either a second step is owed, or the account first has to fix something.</summary>
public sealed record SignInResponse(string Status, string? Scope);

/// <summary>GET /api/auth/me: who the caller is, what the session may do, and the anti-forgery value for unsafe requests (AC-10).</summary>
public sealed record MeResponse(long UserId, string UserName, string DisplayName, string Scope, string CsrfToken);

/// <summary>
/// The <c>/api/auth</c> routes (sessions AC-1, AC-2, AC-3, AC-6 to AC-11). Everything here answers <c>Cache-Control: no-store</c>. The
/// routes that need no session (sign-in, refresh, sign-out) are guarded by the <c>Origin</c> check and <c>SameSite=Strict</c>; the
/// ones that act for a signed-in person also need the anti-forgery header.
/// </summary>
internal static class AuthEndpoints
{
    private const long MaxBodyBytes = 4 * 1024;
    private const string SignInFailedTitle = "Sign-in failed";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapAuthRoutes().WithTags("Identity").WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes));

        auth.MapPost("/sign-in", SignIn).AddEndpointFilter<OriginCheckFilter>();

        // The second step arrives with the mfa feature; until then there is nothing to verify against.
        auth.MapPost("/mfa/verify", (HttpContext http) =>
        {
            SessionCookieWriter.NoStore(http.Response);
            return Results.Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Not available yet");
        }).AddEndpointFilter<OriginCheckFilter>();

        auth.MapPost("/refresh", Refresh).AddEndpointFilter<OriginCheckFilter>();
        auth.MapPost("/sign-out", SignOut).AddEndpointFilter<OriginCheckFilter>();

        auth.MapPost("/sign-out-all", SignOutEverywhere).RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        auth.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> SignIn(
        SignInRequest body, ISender sender, HttpContext http, SessionCookieWriter cookies, TimeProvider clock, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var result = await sender.Send(
            new SignInCommand(
                body.UserName ?? string.Empty,
                new SecretString(body.Password ?? string.Empty),
                http.ClientIp(),
                cookies.ReadAccess(http.Request),
                http.Request.Headers.UserAgent.ToString()),
            cancellationToken);

        switch (result.Status)
        {
            case SignInStatus.PasswordAccepted:
                cookies.WritePending(http.Response, result.PendingToken!, clock.GetUtcNow() + PendingSignIn.Lifetime);
                return Results.Ok(new SignInResponse("mfa_required", null));

            case SignInStatus.LimitedSession:
                cookies.WriteSession(http.Response, result.Tokens!, result.AccessExpiresAt!.Value, result.RefreshExpiresAt!.Value);
                return Results.Ok(new SignInResponse("limited", result.Scope!.Value.ToString()));

            case SignInStatus.Throttled:
                http.Response.Headers.RetryAfter = result.RetryAfterSeconds!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many attempts");

            default: // one answer for every kind of failure (AC-7)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: SignInFailedTitle);
        }
    }

    private static async Task<IResult> Refresh(
        ISender sender, HttpContext http, SessionCookieWriter cookies, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var presented = cookies.ReadRefresh(http.Request);
        if (string.IsNullOrEmpty(presented) || presented.Length > RefreshSessionCommandValidator.MaxLength)
        {
            return EndSession(http, cookies);
        }

        var result = await sender.Send(new RefreshSessionCommand(presented), cancellationToken);
        switch (result.Outcome)
        {
            case RefreshOutcome.Rotated:
                cookies.WriteSession(http.Response, result.Session!.Tokens, result.Session.AccessExpiresAt, result.Session.RefreshExpiresAt);
                return Results.NoContent();

            case RefreshOutcome.ConcurrentRefresh: // another request just rotated it: the browser already holds the new pair, so retry once
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Refresh already in progress");

            default: // unknown, expired, revoked, or a reused value (the session is already revoked)
                return EndSession(http, cookies);
        }
    }

    private static async Task<IResult> SignOut(ISender sender, HttpContext http, SessionCookieWriter cookies, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        await sender.Send(new SignOutCommand(cookies.ReadAccess(http.Request)), cancellationToken);
        cookies.Clear(http.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> SignOutEverywhere(ISender sender, HttpContext http, SessionCookieWriter cookies, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        if (SessionClaims.UserId(http.User) is not { } userId)
        {
            return Results.Unauthorized();
        }

        await sender.Send(new SignOutEverywhereCommand(userId), cancellationToken);
        cookies.Clear(http.Response);
        return Results.NoContent();
    }

    private static IResult Me(HttpContext http, CsrfTokenService csrf)
    {
        SessionCookieWriter.NoStore(http.Response);

        var user = http.User;

        // A Development/Testing host without real authentication signs everyone in as one stand-in user. The admin app asks here on
        // start-up, so it gets a usable answer instead of being sent to a sign-in page that cannot work in that mode.
        if (user.Identity?.AuthenticationType == RompAuthorizationExtensions.DevelopmentScheme)
        {
            return Results.Ok(new MeResponse(0, "dev-staff", "Development user", nameof(SessionScope.Full), string.Empty));
        }

        if (SessionClaims.UserId(user) is not { } userId || SessionClaims.Session(user) is not { } sessionId
            || !Enum.TryParse<SessionScope>(user.FindFirstValue(SessionPrincipal.ScopeClaim), out var scope))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new MeResponse(
            userId,
            user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            user.FindFirstValue(SessionPrincipal.DisplayNameClaim) ?? string.Empty,
            scope.ToString(),
            csrf.Create(sessionId)));
    }

    private static IResult EndSession(HttpContext http, SessionCookieWriter cookies)
    {
        cookies.Clear(http.Response);
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session ended");
    }
}
