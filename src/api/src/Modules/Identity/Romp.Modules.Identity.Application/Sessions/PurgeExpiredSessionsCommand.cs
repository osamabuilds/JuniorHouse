using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Application.Sessions;

/// <summary>How many rows of each kind a purge removed.</summary>
public sealed record PurgeResult(int Sessions, int Attempts, int PendingSignIns);

/// <summary>
/// Removes sign-in history older than 90 days (sessions housekeeping): sessions that could no longer be used 90 days ago (judged by the
/// absolute limit, so a live session is never touched), sign-in attempts, and half-finished sign-ins. Safe to run at any time and any
/// number of times.
/// </summary>
public sealed record PurgeExpiredSessionsCommand : IRequest<PurgeResult>, IIdentityCommand;

public sealed class PurgeExpiredSessionsCommandHandler(IIdentityDbContext dbContext, TimeProvider clock)
    : IRequestHandler<PurgeExpiredSessionsCommand, PurgeResult>
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    public async Task<PurgeResult> Handle(PurgeExpiredSessionsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - Retention;

        var sessions = await dbContext.Sessions.Where(s => s.AbsoluteExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        var attempts = await dbContext.LoginAttempts.Where(a => a.AttemptedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        var pending = await dbContext.PendingSignIns.Where(p => p.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        return new PurgeResult(sessions, attempts, pending);
    }
}
