using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>Body of POST /api/auth/sign-in. Plain strings on the wire; the password becomes a <see cref="SecretString"/> at the edge.</summary>
public sealed record SignInRequest(string? UserName, string? Password);

/// <summary>Answer to a password that was accepted: either a second step is owed, or the account first has to fix something.</summary>
public sealed record SignInResponse(string Status, string? Scope);

/// <summary>Body of the MFA routes that take a code. A missing code is answered as an empty one (a validation error).</summary>
public sealed record MfaCodeRequest(string? Code);

/// <summary>The one answer that shows the secret (AC-2): the address a QR code carries, the same secret as grouped text, and how long it stays valid.</summary>
public sealed record MfaEnrolmentResponse(string OtpAuthUri, string SecretText, DateTimeOffset ExpiresAt);

/// <summary>Recovery codes, shown once (AC-6).</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// GET /api/auth/me: who the caller is, what the session may do, the anti-forgery value for unsafe requests (AC-10), and the permissions the
/// caller holds right now. The permissions are for hiding menus and buttons only; every request is still checked on the server (roles AC-7).
/// </summary>
public sealed record MeResponse(long UserId, string UserName, string DisplayName, string Scope, string CsrfToken, IReadOnlyList<string> Permissions);

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

        auth.MapPost("/mfa/verify", VerifyMfa).AddEndpointFilter<OriginCheckFilter>();

        // Enrolment: only a session that still owes it (scope EnrolMfa) may start or confirm; the anti-forgery header is required.
        auth.MapPost("/mfa/enrol/start", StartEnrolment).RequireAuthorization().AddEndpointFilter<CsrfFilter>().RequireScope(SessionScope.EnrolMfa);
        auth.MapPost("/mfa/enrol/confirm", ConfirmEnrolment).RequireAuthorization().AddEndpointFilter<CsrfFilter>().RequireScope(SessionScope.EnrolMfa);
        auth.MapGet("/mfa", MfaStatusOf).RequireAuthorization().RequireScope(SessionScope.Full, SessionScope.EnrolMfa);
        auth.MapPost("/mfa/recovery-codes", RegenerateCodes).RequireAuthorization().AddEndpointFilter<CsrfFilter>().RequireScope(SessionScope.Full);

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

    private static async Task<IResult> Me(HttpContext http, CsrfTokenService csrf, ISender sender, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var user = http.User;

        // A Development/Testing host without real authentication signs everyone in as one stand-in user. The admin app asks here on
        // start-up, so it gets a usable answer instead of being sent to a sign-in page that cannot work in that mode.
        if (user.Identity?.AuthenticationType == RompAuthorizationExtensions.DevelopmentScheme)
        {
            return Results.Ok(new MeResponse(0, "dev-staff", "Development user", nameof(SessionScope.Full), string.Empty, [.. Permissions.All.Select(p => p.Value)]));
        }

        if (SessionClaims.UserId(user) is not { } userId || SessionClaims.Session(user) is not { } sessionId
            || !Enum.TryParse<SessionScope>(user.FindFirstValue(SessionPrincipal.ScopeClaim), out var scope))
        {
            return Results.Unauthorized();
        }

        // A session that still owes a step may do nothing but that step, so it is told it holds nothing.
        IReadOnlyList<string> permissions = scope == SessionScope.Full
            ? [.. (await sender.Send(new GetEffectivePermissionsQuery(userId), cancellationToken)).Order(StringComparer.Ordinal)]
            : [];
        return Results.Ok(new MeResponse(
            userId,
            user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            user.FindFirstValue(SessionPrincipal.DisplayNameClaim) ?? string.Empty,
            scope.ToString(),
            csrf.Create(sessionId),
            permissions));
    }

    private static async Task<IResult> VerifyMfa(
        MfaCodeRequest body, ISender sender, HttpContext http, SessionCookieWriter cookies, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var pending = cookies.ReadPending(http.Request);
        if (string.IsNullOrEmpty(pending) || pending.Length > 128)
        {
            return EndSignIn(http, cookies);
        }

        var result = await sender.Send(
            new VerifyMfaCommand(pending, body.Code ?? string.Empty, http.ClientIp(), cookies.ReadAccess(http.Request), http.Request.Headers.UserAgent.ToString()),
            cancellationToken);

        switch (result.Status)
        {
            case VerifyMfaStatus.Succeeded:
                cookies.ClearPending(http.Response);
                cookies.WriteSession(http.Response, result.Session!.Tokens, result.Session.AccessExpiresAt, result.Session.RefreshExpiresAt);
                return Results.Ok(new SignInResponse("signed_in", null));

            case VerifyMfaStatus.Throttled:
                http.Response.Headers.RetryAfter = result.RetryAfterSeconds!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many attempts");

            case VerifyMfaStatus.PendingEnded:
                return EndSignIn(http, cookies);

            default: // one answer for a wrong, replayed or malformed code
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "That code is not valid");
        }
    }

    private static async Task<IResult> StartEnrolment(ISender sender, HttpContext http, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var started = await sender.Send(new StartMfaEnrolmentCommand(CallerId(http)), cancellationToken);
        return Results.Ok(new MfaEnrolmentResponse(started.OtpAuthUri.Reveal(), started.SecretText.Reveal(), started.ExpiresAt));
    }

    private static async Task<IResult> ConfirmEnrolment(MfaCodeRequest body, ISender sender, HttpContext http, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var issued = await sender.Send(new ConfirmMfaEnrolmentCommand(CallerId(http), body.Code ?? string.Empty), cancellationToken);
        return Results.Ok(new RecoveryCodesResponse(issued.Codes.Select(c => c.Reveal()).ToList()));
    }

    private static async Task<IResult> MfaStatusOf(ISender sender, HttpContext http, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        return Results.Ok(await sender.Send(new GetMfaStatusQuery(CallerId(http)), cancellationToken));
    }

    private static async Task<IResult> RegenerateCodes(MfaCodeRequest body, ISender sender, HttpContext http, CancellationToken cancellationToken)
    {
        SessionCookieWriter.NoStore(http.Response);

        var issued = await sender.Send(new RegenerateRecoveryCodesCommand(CallerId(http), body.Code ?? string.Empty), cancellationToken);
        return Results.Ok(new RecoveryCodesResponse(issued.Codes.Select(c => c.Reveal()).ToList()));
    }

    /// <summary>The account of the session. Only reached behind authorization and a scope check, so the claim is always there.</summary>
    private static long CallerId(HttpContext http) =>
        SessionClaims.UserId(http.User) ?? throw new InvalidOperationException("An authenticated request has no user id.");

    /// <summary>The pending sign-in is gone (unknown, expired, spent or ended): the person starts again with the password.</summary>
    private static IResult EndSignIn(HttpContext http, SessionCookieWriter cookies)
    {
        cookies.ClearPending(http.Response);
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in again");
    }

    /// <summary>Refuses a session whose scope is not one of the allowed ones: a limited session reaches only the step it owes (AC-1).</summary>
    private static RouteHandlerBuilder RequireScope(this RouteHandlerBuilder builder, params SessionScope[] allowed) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var scope = context.HttpContext.User.FindFirstValue(SessionPrincipal.ScopeClaim);
            return allowed.Any(a => a.ToString() == scope)
                ? await next(context)
                : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
        });

    private static IResult EndSession(HttpContext http, SessionCookieWriter cookies)
    {
        cookies.Clear(http.Response);
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session ended");
    }
}
