using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

public sealed record RegisterRequest(string? Email, string? FirstName, string? LastName, string? Mobile, string? Password, bool KeepSignedIn, bool SubscribeNewsletter = false);

public sealed record SignInRequest(string? Email, string? Password, bool KeepSignedIn, string? CaptchaToken);

public sealed record EmailTokenRequest(string? Token);

public sealed record EmailRequest(string? Email);

public sealed record ResetPasswordRequest(string? Token, string? NewPassword);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record ChangeEmailRequest(string? NewEmail, string? CurrentPassword);

/// <summary>What the page needs after sign-in: a greeting, whether to show the unverified-email notice (ACT-31), and the anti-forgery value for unsafe requests.</summary>
public sealed record AuthSessionResponse(string FirstName, bool EmailVerified, string CsrfToken);

public sealed record PasswordRuleResponse(string Code, string Message);

/// <summary>
/// The customer sign-in routes (accounts ACT-1 to ACT-14, ACT-27, ACT-28). They live under <c>/api/customers/auth</c> because
/// <c>/api/auth</c> is the staff area. Everything answers <c>Cache-Control: no-store</c> and shares one strict per-address rate limit. The routes
/// that need no session are guarded by the <c>Origin</c> check; the ones that act for a signed-in customer also need the anti-forgery header.
/// No route takes a customer id: the caller is the customer (SEC-05).
/// </summary>
internal static class CustomerAuthEndpoints
{
    public const string Prefix = "/api/customers/auth";
    private const long MaxBodyBytes = 4 * 1024;

    public static void MapCustomerAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var open = endpoints.MapSecuredGroup(Prefix, Policies.Public)
            .RequireRateLimiting(RateLimitPolicies.Strict)
            .WithTags("Customers")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CustomerNoStoreFilter>();

        open.MapPost("/register", Register).AddEndpointFilter<CustomerOriginCheckFilter>();
        open.MapPost("/sign-in", SignIn).AddEndpointFilter<CustomerOriginCheckFilter>();
        open.MapPost("/email/verify", VerifyEmail).AddEndpointFilter<CustomerOriginCheckFilter>();
        open.MapPost("/password-reset/request", RequestReset).AddEndpointFilter<CustomerOriginCheckFilter>();
        open.MapPost("/password-reset/confirm", ConfirmReset).AddEndpointFilter<CustomerOriginCheckFilter>();

        var mine = endpoints.MapSecuredGroup(Prefix, Policies.Customer)
            .RequireRateLimiting(RateLimitPolicies.Strict)
            .WithTags("Customers")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CustomerNoStoreFilter>();

        mine.MapPost("/email/resend", ResendVerification).AddEndpointFilter<CustomerCsrfFilter>();
        mine.MapPost("/email/change", ChangeEmail).AddEndpointFilter<CustomerCsrfFilter>();
        mine.MapPost("/password/change", ChangePassword).AddEndpointFilter<CustomerCsrfFilter>();

        // Called on every page view (who is signed in, renew cookies) or cheap and harmless: they stay under the host-wide limit only, so a few
        // visitors behind one address never use up the strict allowance that guards register, sign-in and reset.
        var light = endpoints.MapSecuredGroup(Prefix, Policies.Public)
            .WithTags("Customers")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CustomerNoStoreFilter>();

        light.MapGet("/password-rules", () => Results.Ok(PasswordRule.Check(null).Select(c => new PasswordRuleResponse(c.Code, c.Message))));
        light.MapPost("/refresh", Refresh).AddEndpointFilter<CustomerOriginCheckFilter>();
        light.MapPost("/sign-out", SignOut).AddEndpointFilter<CustomerOriginCheckFilter>();

        var session = endpoints.MapSecuredGroup(Prefix, Policies.Customer)
            .WithTags("Customers")
            .AddEndpointFilter<CustomerNoStoreFilter>();
        session.MapGet("/session", Session);
    }

    private static async Task<IResult> Register(
        RegisterRequest body, ISender sender, HttpContext http, CustomerCookies cookies, CustomerCsrfTokens csrf, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterCommand(
                body.Email ?? string.Empty,
                body.FirstName ?? string.Empty,
                body.LastName ?? string.Empty,
                body.Mobile ?? string.Empty,
                new SecretString(body.Password ?? string.Empty),
                body.KeepSignedIn,
                http.ClientIp(),
                http.Request.Headers.UserAgent.ToString(),
                cookies.ReadAccess(http.Request),
                CustomerCookies.ReadGuestSession(http.Request),
                body.SubscribeNewsletter),
            cancellationToken);

        switch (result.Status)
        {
            case RegisterStatus.Registered:
                cookies.WriteSession(http.Response, result.Session!);
                CustomerCookies.ClearGuest(http.Response);
                return Results.Json(Response(result.Session!, csrf), statusCode: StatusCodes.Status201Created);

            case RegisterStatus.PasswordRejected:
                return Results.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "The password does not meet the rules",
                    extensions: new Dictionary<string, object?> { ["unmetRules"] = result.UnmetRules });

            default: // the email is taken; the spec says to tell the customer so they can sign in or reset (ACT-3)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "That email address already has an account");
        }
    }

    private static async Task<IResult> SignIn(
        SignInRequest body, ISender sender, HttpContext http, CustomerCookies cookies, CustomerCsrfTokens csrf, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SignInCommand(
                body.Email ?? string.Empty,
                new SecretString(body.Password ?? string.Empty),
                body.KeepSignedIn,
                body.CaptchaToken,
                http.ClientIp(),
                cookies.ReadAccess(http.Request),
                http.Request.Headers.UserAgent.ToString(),
                CustomerCookies.ReadGuestSession(http.Request)),
            cancellationToken);

        switch (result.Status)
        {
            case SignInStatus.SignedIn:
                cookies.WriteSession(http.Response, result.Session!);
                CustomerCookies.ClearGuest(http.Response);
                return Results.Ok(Response(result.Session!, csrf));

            case SignInStatus.Throttled:
                http.Response.Headers.RetryAfter = result.RetryAfterSeconds!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many attempts",
                    extensions: new Dictionary<string, object?> { ["captchaRequired"] = true });

            case SignInStatus.CaptchaRequired:
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "We could not sign you in",
                    extensions: new Dictionary<string, object?> { ["captchaRequired"] = true });

            default: // one answer for every kind of failure (ACT-5)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "We could not sign you in");
        }
    }

    private static async Task<IResult> Refresh(ISender sender, HttpContext http, CustomerCookies cookies, CancellationToken cancellationToken)
    {
        var presented = cookies.ReadRefresh(http.Request);
        if (string.IsNullOrEmpty(presented) || presented.Length > CustomerSessionIssuer.MaxPresentedLength)
        {
            return EndSession(http, cookies);
        }

        var result = await sender.Send(new RefreshSessionCommand(presented), cancellationToken);
        switch (result.Outcome)
        {
            case Domain.Accounts.RefreshOutcome.Rotated:
                cookies.WriteSession(http.Response, result.Session!);
                CustomerCookies.ClearGuest(http.Response);
                return Results.NoContent();

            case Domain.Accounts.RefreshOutcome.ConcurrentRefresh: // another request just rotated it: the browser already holds the new pair, so retry once
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Refresh already in progress");

            default: // unknown, expired, revoked, or a reused value (the session is already revoked)
                return EndSession(http, cookies);
        }
    }

    private static async Task<IResult> SignOut(ISender sender, HttpContext http, CustomerCookies cookies, CancellationToken cancellationToken)
    {
        await sender.Send(new SignOutCommand(cookies.ReadAccess(http.Request)), cancellationToken);
        cookies.Clear(http.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> VerifyEmail(EmailTokenRequest body, ISender sender, CancellationToken cancellationToken) =>
        await sender.Send(new VerifyEmailCommand(body.Token ?? string.Empty), cancellationToken) == VerifyEmailResult.Verified
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This link is invalid or has expired");

    private static async Task<IResult> RequestReset(EmailRequest body, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new RequestPasswordResetCommand(body.Email ?? string.Empty), cancellationToken);
        return Results.Accepted(); // the same answer whether or not the address has an account (ACT-6)
    }

    private static async Task<IResult> ConfirmReset(ResetPasswordRequest body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResetPasswordCommand(body.Token ?? string.Empty, new SecretString(body.NewPassword ?? string.Empty)), cancellationToken);
        return result.Status switch
        {
            ResetPasswordStatus.Reset => Results.NoContent(),
            ResetPasswordStatus.PasswordRejected => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The password does not meet the rules",
                extensions: new Dictionary<string, object?> { ["unmetRules"] = result.UnmetRules }),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This link is invalid or has expired"),
        };
    }

    private static async Task<IResult> Session(ISender sender, HttpContext http, CustomerCsrfTokens csrf, CancellationToken cancellationToken)
    {
        var me = await sender.Send(new GetCurrentCustomerQuery(), cancellationToken);
        var sessionId = CustomerSessionPrincipal.SessionId(http.User);
        return sessionId is null
            ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Not signed in")
            : Results.Ok(new AuthSessionResponse(me.FirstName, me.EmailVerified, csrf.Create(sessionId.Value)));
    }

    private static async Task<IResult> ResendVerification(ISender sender, CancellationToken cancellationToken) =>
        await sender.Send(new SendEmailVerificationCommand(), cancellationToken) switch
        {
            SendVerificationStatus.Sent => Results.Accepted(),
            SendVerificationStatus.AlreadyVerified => Results.NoContent(),
            _ => Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many links requested, try again later"),
        };

    private static async Task<IResult> ChangeEmail(ChangeEmailRequest body, ISender sender, CancellationToken cancellationToken) =>
        await sender.Send(new ChangeEmailCommand(body.NewEmail ?? string.Empty, new SecretString(body.CurrentPassword ?? string.Empty)), cancellationToken) switch
        {
            ChangeEmailStatus.Changed => Results.Accepted(),
            ChangeEmailStatus.EmailTaken => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "That email address already has an account"),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The password is not correct"),
        };

    private static async Task<IResult> ChangePassword(ChangePasswordRequest body, ISender sender, HttpContext http, CustomerCookies cookies, CancellationToken cancellationToken) =>
        await sender.Send(
            new ChangePasswordCommand(new SecretString(body.CurrentPassword ?? string.Empty), new SecretString(body.NewPassword ?? string.Empty), cookies.ReadAccess(http.Request)),
            cancellationToken) switch
        {
            ChangePasswordStatus.Changed => Results.NoContent(),
            ChangePasswordStatus.PasswordRejected => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "The password does not meet the rules"),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The current password is not correct"),
        };

    private static AuthSessionResponse Response(StartedSession session, CustomerCsrfTokens csrf) =>
        new(session.FirstName, session.EmailVerified, csrf.Create(session.SessionId));

    /// <summary>The session cannot be used any more: the cookies are removed so the browser stops sending them, and the caller is unauthenticated.</summary>
    private static IResult EndSession(HttpContext http, CustomerCookies cookies)
    {
        cookies.Clear(http.Response);
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Not signed in");
    }
}
