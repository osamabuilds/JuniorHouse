using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Application.Sessions;

/// <summary>
/// The real <see cref="IUserSessionRevoker"/>: ends every active session of an account inside the caller's transaction, so a
/// deactivation or password change and the end of the sessions commit together and take effect on the next request (AC-9).
/// A session that already ended keeps its first date and reason.
/// </summary>
public sealed class SessionRevoker(IIdentityDbContext dbContext, TimeProvider clock) : IUserSessionRevoker
{
    public async Task RevokeAsync(long userId, long? exceptSessionId, SessionRevokeReason reason, CancellationToken cancellationToken) =>
        await RevokeActiveAsync(dbContext, clock, userId, exceptSessionId, reason, cancellationToken);

    internal static async Task<int> RevokeActiveAsync(
        IIdentityDbContext dbContext, TimeProvider clock, long userId, long? exceptSessionId, SessionRevokeReason reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var open = await dbContext.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != exceptSessionId)
            .ToListAsync(cancellationToken);
        foreach (var session in open)
        {
            session.Revoke(reason, now);
        }

        return open.Count;
    }
}

/// <summary>Ends the session that holds this access cookie (AC-11). Idempotent: an unknown, missing or already ended session is not an error.</summary>
public sealed record SignOutCommand(string? AccessToken) : IRequest, IIdentityCommand;

public sealed class SignOutCommandHandler(IIdentityDbContext dbContext, TimeProvider clock) : IRequestHandler<SignOutCommand>
{
    private const int MaxPresentedLength = 128;

    public async Task Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        var presented = request.AccessToken;
        if (string.IsNullOrEmpty(presented) || presented.Length > MaxPresentedLength)
        {
            return;
        }

        var hash = SessionTokens.Hash(presented);
        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.AccessTokenHash == hash, cancellationToken);
        session?.Revoke(SessionRevokeReason.SignOut, clock.GetUtcNow());
    }
}

/// <summary>Ends every session of the account; returns how many were still active (AC-11).</summary>
public sealed record SignOutEverywhereCommand(long UserId) : IRequest<int>, IIdentityCommand;

public sealed class SignOutEverywhereCommandHandler(IIdentityDbContext dbContext, TimeProvider clock) : IRequestHandler<SignOutEverywhereCommand, int>
{
    public Task<int> Handle(SignOutEverywhereCommand request, CancellationToken cancellationToken) =>
        SessionRevoker.RevokeActiveAsync(dbContext, clock, request.UserId, exceptSessionId: null, SessionRevokeReason.SignOutEverywhere, cancellationToken);
}
