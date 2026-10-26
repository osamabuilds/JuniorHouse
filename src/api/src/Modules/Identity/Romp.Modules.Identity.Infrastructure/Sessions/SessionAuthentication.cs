using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>
/// The caller as the rest of the host sees them: who and which session, plus the scope. Permission and role claims are left out on
/// purpose; they are evaluated per request from the database so a role change applies at once (roles AC-5).
/// </summary>
public static class SessionPrincipal
{
    public const string Scheme = RompAuthorizationExtensions.SessionScheme;

    public const string DisplayNameClaim = "display_name";

    public const string ScopeClaim = "scope";

    public static ClaimsPrincipal Create(AuthenticatedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(SessionClaims.SessionId, session.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, session.UserName),
                new Claim(DisplayNameClaim, session.DisplayName),
                new Claim(ScopeClaim, session.Scope.ToString()),
            ],
            Scheme);
        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Authenticates a request from its access cookie by asking <see cref="AuthenticateSessionQuery"/> (AC-9). No cookie, or a session
/// that cannot be used, means no result, which the pipeline turns into 401.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISender sender,
    IOptions<IdentityOptions> identityOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionCookieNames.For(identityOptions.Value.AllowInsecureCookies).Access, out var value))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await sender.Send(new AuthenticateSessionQuery(value), Context.RequestAborted);
        return session is null
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Success(new AuthenticationTicket(SessionPrincipal.Create(session), Scheme.Name));
    }
}
