using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Application.Sessions;

/// <summary>Who a request comes from and what it may do. Carries no permissions: those are looked up on each request (roles AC-5).</summary>
public sealed record AuthenticatedSession(long UserId, long SessionId, string UserName, string DisplayName, SessionScope Scope);

/// <summary>
/// Turns the access cookie value into the caller, or null when the request has no usable session (AC-9). Null covers a missing,
/// unknown, revoked or expired session and an inactive account alike; the caller answers 401 and never says which.
/// </summary>
public sealed record AuthenticateSessionQuery(string? AccessToken) : IRequest<AuthenticatedSession?>;

/// <summary>
/// One query loads the session together with its user. Whether the session is usable is decided by <see cref="Session"/> itself, so
/// the rules (revoked, idle, absolute, access expiry) live in one place. Nothing is written: activity does not extend a session,
/// only a refresh does.
/// </summary>
public sealed class AuthenticateSessionQueryHandler(IIdentityDbContext dbContext, IMfaEnrolmentStatus mfa, TimeProvider clock)
    : IRequestHandler<AuthenticateSessionQuery, AuthenticatedSession?>
{
    /// <summary>The longest text that can be a token; longer values are never looked up.</summary>
    private const int MaxPresentedLength = 128;

    public async Task<AuthenticatedSession?> Handle(AuthenticateSessionQuery request, CancellationToken cancellationToken)
    {
        var presented = request.AccessToken;
        if (string.IsNullOrWhiteSpace(presented) || presented.Length > MaxPresentedLength)
        {
            return null;
        }

        var hash = SessionTokens.Hash(presented);
        var row = await dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.AccessTokenHash == hash)
            .Join(
                dbContext.Staff,
                s => s.UserId,
                u => u.Id,
                (s, u) => new { Session = s, u.UserName, u.DisplayName, u.IsActive, u.MustChangePassword })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null || !row.IsActive || !row.Session.AccessTokenMatches(presented) || !row.Session.AccessIsValid(clock.GetUtcNow()))
        {
            return null;
        }

        var scope = SessionScopes.For(row.MustChangePassword, await mfa.IsEnrolledAsync(row.Session.UserId, cancellationToken));
        return new AuthenticatedSession(row.Session.UserId, row.Session.Id, row.UserName, row.DisplayName, scope);
    }
}
