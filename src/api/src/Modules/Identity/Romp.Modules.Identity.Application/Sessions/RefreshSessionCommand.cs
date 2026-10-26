using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Application.Sessions;

/// <summary>The new cookie values (shown once) and when each part ends.</summary>
public sealed record RefreshedSession(long SessionId, IssuedTokens Tokens, DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt);

/// <summary><c>Session</c> is set only when the outcome is <see cref="RefreshOutcome.Rotated"/>.</summary>
public sealed record RefreshSessionResult(RefreshOutcome Outcome, RefreshedSession? Session);

/// <summary>Exchanges the refresh cookie for a new pair (AC-2). Reuse of an old value revokes the session (AC-3).</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<RefreshSessionResult>, IIdentityCommand;

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    /// <summary>The longest text that can be a token; longer values are never looked up.</summary>
    public const int MaxLength = 128;

    public RefreshSessionCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(MaxLength);
    }
}

/// <summary>
/// The rotation is a conditional update: the row carries a PostgreSQL <c>xmin</c> concurrency token, so the UPDATE only matches if
/// nobody changed the session since it was read. Of several requests with the same cookie one wins; the others find the row changed
/// and answer <see cref="RefreshOutcome.ConcurrentRefresh"/>, which never revokes (the grace is in <see cref="Session.Rotate"/>).
/// </summary>
public sealed class RefreshSessionCommandHandler(IIdentityDbContext dbContext, SessionLifetimes lifetimes, TimeProvider clock)
    : IRequestHandler<RefreshSessionCommand, RefreshSessionResult>
{
    public async Task<RefreshSessionResult> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var hash = SessionTokens.Hash(request.RefreshToken);
        var session = await dbContext.Sessions.FirstOrDefaultAsync(
            s => s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash, cancellationToken);
        if (session is null)
        {
            return new RefreshSessionResult(RefreshOutcome.Unusable, null);
        }

        var result = session.Rotate(request.RefreshToken, clock.GetUtcNow(), lifetimes);
        if (result.Outcome is not (RefreshOutcome.Rotated or RefreshOutcome.ReuseDetected))
        {
            return new RefreshSessionResult(result.Outcome, null);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else changed this session first (another refresh, a sign-out). Drop our change; the client may retry once.
            dbContext.Discard(session);
            return new RefreshSessionResult(RefreshOutcome.ConcurrentRefresh, null);
        }

        return result.Outcome == RefreshOutcome.Rotated
            ? new RefreshSessionResult(RefreshOutcome.Rotated, new RefreshedSession(session.Id, result.Tokens!, session.AccessExpiresAt, session.RefreshExpiresAt))
            : new RefreshSessionResult(result.Outcome, null);
    }
}
