using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Domain.Sessions;

namespace Romp.Modules.Identity.Application.Mfa;

public enum VerifyMfaStatus
{
    /// <summary>The code (or recovery code) was valid: a full session was issued.</summary>
    Succeeded = 1,

    /// <summary>One answer for a wrong code, a replayed code and a malformed one (AC-4); the person may try again.</summary>
    Failed = 2,

    /// <summary>A delay is running for this name and address (sessions AC-6); answer 429 with <see cref="VerifyMfaResult.RetryAfterSeconds"/>.</summary>
    Throttled = 3,

    /// <summary>
    /// The pending sign-in is unknown, expired, spent, belongs to an account that can no longer sign in, or was just ended by the fifth
    /// wrong code (AC-5): the person has to start again with the password.
    /// </summary>
    PendingEnded = 4,
}

public sealed record VerifyMfaResult(VerifyMfaStatus Status, IssuedSession? Session, int? RetryAfterSeconds, bool UsedRecoveryCode = false)
{
    public static readonly VerifyMfaResult Failure = new(VerifyMfaStatus.Failed, null, null);

    public static readonly VerifyMfaResult Ended = new(VerifyMfaStatus.PendingEnded, null, null);
}

/// <summary>
/// Step 2 of sign-in (mfa AC-3 to AC-6): the pending token from the password step plus an authenticator code or a recovery code.
/// <paramref name="PresentedAccessToken"/> is the session cookie that came with the request; it is revoked when the session is issued.
/// </summary>
public sealed record VerifyMfaCommand(string PendingToken, string Code, string ClientIp, string? PresentedAccessToken, string? UserAgent)
    : IRequest<VerifyMfaResult>, IIdentityCommand;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(c => c.PendingToken).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(32);
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(Session.ClientIpMaxLength);
    }
}

public sealed class VerifyMfaCommandHandler(
    IIdentityDbContext dbContext,
    ISecretProtector protector,
    IRecoveryPepper pepper,
    SignInThrottle throttle,
    SessionIssuer sessions,
    TimeProvider clock,
    ILoggerFactory loggers) : IRequestHandler<VerifyMfaCommand, VerifyMfaResult>
{
    private readonly ILogger _security = loggers.CreateLogger(SecurityEvents.Category);

    public async Task<VerifyMfaResult> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = SessionTokens.Hash(request.PendingToken);
        var pending = await dbContext.PendingSignIns.FirstOrDefaultAsync(p => p.TokenHash == hash, cancellationToken);
        if (pending is null)
        {
            return VerifyMfaResult.Ended;
        }

        var user = await dbContext.Staff.FirstOrDefaultAsync(u => u.Id == pending.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsMfaEnrolled || user.MfaSecretCipher is null || user.MfaKeyId is null || pending.IsExpired(now))
        {
            dbContext.PendingSignIns.Remove(pending);
            return VerifyMfaResult.Ended;
        }

        var typed = AttemptNames.Sanitise(user.UserName);
        if (await throttle.WaitAsync(typed, request.ClientIp, now, cancellationToken) is { } wait)
        {
            // Not a failure and not a success: it neither lengthens the delay nor uses up one of the five tries.
            dbContext.LoginAttempts.Add(LoginAttempt.Record(typed, request.ClientIp, LoginOutcome.Throttled, user.Id, now));
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            _security.SignInThrottled(seconds, request.ClientIp);
            return new VerifyMfaResult(VerifyMfaStatus.Throttled, null, seconds);
        }

        var (accepted, usedRecovery) = await TryAcceptAsync(user, request.Code, now, cancellationToken);
        if (!accepted)
        {
            dbContext.LoginAttempts.Add(LoginAttempt.Record(typed, request.ClientIp, LoginOutcome.MfaBad, user.Id, now));
            var ended = pending.RecordFailure();
            if (ended)
            {
                dbContext.PendingSignIns.Remove(pending);
            }

            _security.MfaFailed(ended ? "mfa_pending_ended" : "mfa_bad_code", user.Id, request.ClientIp);
            return ended ? VerifyMfaResult.Ended : VerifyMfaResult.Failure;
        }

        dbContext.LoginAttempts.Add(LoginAttempt.Record(typed, request.ClientIp, LoginOutcome.MfaOk, user.Id, now));
        dbContext.PendingSignIns.Remove(pending);
        var session = await sessions.IssueAsync(user, request.PresentedAccessToken, request.ClientIp, request.UserAgent, cancellationToken);
        return new VerifyMfaResult(VerifyMfaStatus.Succeeded, session, null, usedRecovery);
    }

    /// <summary>
    /// Checks the code and, when it is valid, records its use. The record is saved here, before anything else changes, because that
    /// write is the guard against replay: it only matches if nobody changed the row since it was read (the PostgreSQL <c>xmin</c>
    /// token), so of several requests with one code exactly one is accepted and the rest find the row changed (AC-4, AC-6).
    /// </summary>
    private async Task<(bool Accepted, bool UsedRecoveryCode)> TryAcceptAsync(
        Domain.StaffAccounts.StaffUser user, string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (TotpVerifier.LooksLikeACode(code))
        {
            var secret = protector.Unprotect(new ProtectedSecret(user.MfaKeyId!, user.MfaSecretCipher!), MfaBinding.For(user.Id));
            if (TotpVerifier.Verify(secret, code, now, user.MfaLastStep) is not { } step || !user.TryAcceptMfaStep(step))
            {
                return (false, false);
            }

            return await SaveGuardedAsync(user, usedRecovery: false, cancellationToken);
        }

        if (RecoveryCodes.LooksLikeACode(code))
        {
            var rows = await dbContext.RecoveryCodes.Where(c => c.UserId == user.Id).ToListAsync(cancellationToken);
            return RecoveryCodes.TryUse(rows, user.Id, pepper.Value, code, now) is { } used
                ? await SaveGuardedAsync(used, usedRecovery: true, cancellationToken)
                : (false, false);
        }

        return (false, false);
    }

    private async Task<(bool Accepted, bool UsedRecoveryCode)> SaveGuardedAsync(object changed, bool usedRecovery, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (true, usedRecovery);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Discard(changed); // someone else used this code first
            return (false, false);
        }
    }
}
