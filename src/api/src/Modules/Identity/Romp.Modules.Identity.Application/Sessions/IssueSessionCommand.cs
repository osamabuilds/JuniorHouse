using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.Sessions;

/// <summary>Whether an account has a confirmed second factor. Declared here, implemented by the mfa feature (an in-module port).</summary>
public interface IMfaEnrolmentStatus
{
    Task<bool> IsEnrolledAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>A session just started: the cookie values (shown once) and when each part ends.</summary>
public sealed record IssuedSession(
    long SessionId, IssuedTokens Tokens, SessionScope Scope, DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt);

/// <summary>
/// Starts a session for an account that has passed every sign-in step (AC-1, AC-4). Called from sign-in for a limited account and by
/// the MFA step for everyone else. <paramref name="PresentedAccessToken"/> is the session cookie that came with the request, if any.
/// </summary>
public sealed record IssueSessionCommand(long UserId, string? PresentedAccessToken, string ClientIp, string? UserAgent)
    : IRequest<IssuedSession>, IIdentityCommand;

public sealed class IssueSessionCommandValidator : AbstractValidator<IssueSessionCommand>
{
    public IssueSessionCommandValidator()
    {
        RuleFor(c => c.UserId).GreaterThan(0);
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(Session.ClientIpMaxLength);
    }
}

public sealed class IssueSessionCommandHandler(IIdentityDbContext dbContext, SessionIssuer issuer) : IRequestHandler<IssueSessionCommand, IssuedSession>
{
    public async Task<IssuedSession> Handle(IssueSessionCommand request, CancellationToken cancellationToken)
    {
        var user = await StaffLookups.GetAsync(dbContext, request.UserId, cancellationToken);
        return await issuer.IssueAsync(user, request.PresentedAccessToken, request.ClientIp, request.UserAgent, cancellationToken);
    }
}

/// <summary>
/// The one place a session is started, so every path gets the same rules: a session cookie sent with the request is revoked first and
/// its value never reused (session fixation, SEC-04), the new session always has new values, and a person never holds more than
/// <see cref="Session.MaxPerUser"/> sessions (the oldest are revoked).
/// </summary>
public sealed class SessionIssuer(
    IIdentityDbContext dbContext,
    IMfaEnrolmentStatus mfa,
    SessionLifetimes lifetimes,
    TimeProvider clock)
{
    /// <summary>The longest text that can be a token; longer values are not looked up.</summary>
    private const int MaxPresentedLength = 128;

    public async Task<IssuedSession> IssueAsync(
        StaffUser user, string? presentedAccessToken, string clientIp, string? userAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.IsActive)
        {
            throw new InvalidOperationException("An inactive account cannot start a session.");
        }

        var now = clock.GetUtcNow();
        await RevokePresentedAsync(presentedAccessToken, now, cancellationToken);

        var open = await dbContext.Sessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null && s.AbsoluteExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var oldest in Session.SelectToEvict(open, now))
        {
            oldest.Revoke(SessionRevokeReason.SessionLimit, now);
        }

        var (session, tokens) = Session.Start(user.Id, now, lifetimes, clientIp, userAgent);
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken); // the caller needs the new session's id

        var scope = SessionScopes.For(user.MustChangePassword, await mfa.IsEnrolledAsync(user.Id, cancellationToken));
        return new IssuedSession(session.Id, tokens, scope, session.AccessExpiresAt, session.RefreshExpiresAt);
    }

    /// <summary>Whatever session the request carried ends, whoever it belonged to; an unknown or oversized value is ignored.</summary>
    private async Task RevokePresentedAsync(string? presented, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(presented) || presented.Length > MaxPresentedLength)
        {
            return;
        }

        var hash = SessionTokens.Hash(presented);
        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.AccessTokenHash == hash, cancellationToken);
        session?.Revoke(SessionRevokeReason.NewSignIn, now);
    }
}
