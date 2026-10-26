using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.Sessions;

public enum SignInStatus
{
    /// <summary>The password step passed; the person now owes the MFA step with <see cref="SignInResult.PendingToken"/>.</summary>
    PasswordAccepted = 1,

    /// <summary>One answer for an unknown name, a wrong password, an inactive account and a spent or expired temporary password (AC-7).</summary>
    Failed = 2,

    /// <summary>A delay is running for this name and address, or for the name overall (AC-6); answer 429 with <see cref="SignInResult.RetryAfterSeconds"/>.</summary>
    Throttled = 3,

    /// <summary>
    /// The password passed but the account must first change its password or enrol a second factor, so there is no MFA step: it gets a
    /// session that can do only that (<see cref="SignInResult.Scope"/>).
    /// </summary>
    LimitedSession = 4,
}

/// <summary>
/// The outcome of the password step. <see cref="Failed"/> is a single shared value, so the result of every kind of failure is equal
/// (AC-7). The token is a <see cref="SecretString"/>: it prints as a mask and cannot be serialised.
/// </summary>
public sealed record SignInResult(
    SignInStatus Status,
    long? UserId,
    SecretString? PendingToken,
    int? RetryAfterSeconds,
    long? SessionId = null,
    IssuedTokens? Tokens = null,
    SessionScope? Scope = null,
    DateTimeOffset? AccessExpiresAt = null,
    DateTimeOffset? RefreshExpiresAt = null)
{
    public static readonly SignInResult Failure = new(SignInStatus.Failed, null, null, null);

    public static SignInResult Accepted(long userId, SecretString pendingToken) => new(SignInStatus.PasswordAccepted, userId, pendingToken, null);

    public static SignInResult Throttle(TimeSpan wait) => new(SignInStatus.Throttled, null, null, (int)Math.Ceiling(wait.TotalSeconds));

    public static SignInResult Limited(long userId, IssuedSession session) =>
        new(SignInStatus.LimitedSession, userId, null, null, session.SessionId, session.Tokens, session.Scope, session.AccessExpiresAt, session.RefreshExpiresAt);
}

/// <summary>
/// Step 1 of staff sign-in: user name and password (sessions AC-6, AC-7, AC-8). <paramref name="ClientIp"/> is the real visitor address,
/// worked out by the caller from trusted forwarded headers only (AC-15). <paramref name="PresentedAccessToken"/> is the session cookie
/// that came with the request; it is revoked when a session is issued (AC-4).
/// </summary>
public sealed record SignInCommand(
    string UserName, SecretString Password, string ClientIp, string? PresentedAccessToken = null, string? UserAgent = null)
    : IRequest<SignInResult>, IIdentityCommand;

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        // The name and password are not validated further: a bad one is an ordinary failed attempt, not a different answer (AC-7).
        RuleFor(c => c.UserName).NotNull();
        RuleFor(c => c.Password).NotNull();
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(Session.ClientIpMaxLength);
    }
}

/// <summary>
/// A real hash of a random value, verified against when the name is unknown, so an unknown name costs the same time as a known one
/// (AC-7). Built with the current hasher settings when first needed and kept for the life of the process.
/// </summary>
public sealed class SignInDummyHash(IPasswordHasher hasher)
{
    private readonly Lazy<string> _value = new(() => hasher.Hash(new SecretString(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))));

    public string Value => _value.Value;
}

public sealed class SignInCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher hasher,
    SignInDummyHash dummyHash,
    IMfaEnrolmentStatus mfa,
    SessionIssuer sessions,
    SignInThrottle throttle,
    TimeProvider clock,
    ILoggerFactory loggers) : IRequestHandler<SignInCommand, SignInResult>
{
    private readonly ILogger _security = loggers.CreateLogger(SecurityEvents.Category);

    public async Task<SignInResult> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var typed = AttemptNames.Sanitise(request.UserName);

        var wait = await throttle.WaitAsync(typed, request.ClientIp, now, cancellationToken);
        if (wait is { } remaining)
        {
            Record(typed, request.ClientIp, LoginOutcome.Throttled, null, now);
            _security.SignInThrottled((int)Math.Ceiling(remaining.TotalSeconds), request.ClientIp);
            return SignInResult.Throttle(remaining);
        }

        var user = await FindAsync(typed, cancellationToken);

        // Always verify against something, so the time spent does not say whether the name exists or the account is usable.
        var passwordMatches = hasher.Verify(request.Password, user?.PasswordHash ?? dummyHash.Value);

        if (user is null || !passwordMatches)
        {
            Record(typed, request.ClientIp, LoginOutcome.BadCredentials, user?.Id, now);
            _security.SignInFailed("bad_credentials", user?.Id ?? 0, request.ClientIp);
            return SignInResult.Failure;
        }

        if (!user.IsActive)
        {
            Record(typed, request.ClientIp, LoginOutcome.Inactive, user.Id, now);
            _security.SignInFailed("inactive", user.Id, request.ClientIp);
            return SignInResult.Failure;
        }

        // A generated password allows exactly one sign-in, within its window (staff-accounts AC-4, AC-6).
        if (user.HasTemporaryPassword && !user.TryUseTemporaryPassword(now))
        {
            Record(typed, request.ClientIp, LoginOutcome.TemporaryExpired, user.Id, now);
            _security.SignInFailed("temporary_expired", user.Id, request.ClientIp);
            return SignInResult.Failure;
        }

        Record(typed, request.ClientIp, LoginOutcome.Ok, user.Id, now);

        // An account that still has to change its password or enrol MFA cannot do the MFA step, so it gets a limited session now.
        var scope = SessionScopes.For(user.MustChangePassword, await mfa.IsEnrolledAsync(user.Id, cancellationToken));
        if (scope != SessionScope.Full)
        {
            var limited = await sessions.IssueAsync(user, request.PresentedAccessToken, request.ClientIp, request.UserAgent, cancellationToken);
            return SignInResult.Limited(user.Id, limited);
        }

        var (pending, token) = PendingSignIn.Start(user.Id, now);
        dbContext.PendingSignIns.Add(pending);
        return SignInResult.Accepted(user.Id, token);
    }

    private void Record(string typed, string clientIp, LoginOutcome outcome, long? userId, DateTimeOffset now) =>
        dbContext.LoginAttempts.Add(LoginAttempt.Record(typed, clientIp, outcome, userId, now));

    private async Task<StaffUser?> FindAsync(string typed, CancellationToken cancellationToken)
    {
        string name;
        try
        {
            name = Domain.StaffAccounts.UserName.Create(typed).Value;
        }
        catch (DomainException)
        {
            return null; // not even a possible user name
        }

        return await dbContext.Staff.FirstOrDefaultAsync(u => u.UserName == name, cancellationToken);
    }
}

/// <summary>What is written to the attempt log as the typed name (AC-8).</summary>
internal static class AttemptNames
{
    /// <summary>
    /// Control characters removed, trimmed, truncated to 40 and lower-cased. Lower-casing is deliberate: user names are lower-case, so
    /// "Admin" and "ADMIN" must share one throttle, or capitals would dodge it.
    /// </summary>
    public static string Sanitise(string typed)
    {
        var cleaned = new string(typed.Where(c => !char.IsControl(c)).ToArray()).Trim().ToLowerInvariant();
        return cleaned.Length > LoginAttempt.NameMaxLength ? cleaned[..LoginAttempt.NameMaxLength] : cleaned;
    }
}
