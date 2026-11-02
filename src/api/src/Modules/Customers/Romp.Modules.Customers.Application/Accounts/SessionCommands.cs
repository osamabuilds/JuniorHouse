using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Accounts;

public enum RegisterStatus
{
    Registered = 1,

    /// <summary>The email already belongs to an account (compared case-insensitively, trimmed); the customer is told to sign in or reset (ACT-3).</summary>
    EmailTaken = 2,

    /// <summary>The password breaks the rule; <see cref="RegisterResult.UnmetRules"/> names each broken rule (ACT-2).</summary>
    PasswordRejected = 3,
}

public sealed record RegisterResult(RegisterStatus Status, IReadOnlyList<string> UnmetRules, StartedSession? Session)
{
    public static RegisterResult Taken { get; } = new(RegisterStatus.EmailTaken, [], null);
}

/// <summary>
/// Registration in one step (ACT-1, ACT-29): the account is created and the customer signed in as soon as the form is valid. The mobile
/// number is format-checked and stored, not verified.
/// </summary>
public sealed record RegisterCommand(
    string Email, string FirstName, string LastName, string Mobile, SecretString Password, bool KeepSignedIn, string? ClientIp, string? UserAgent, string? PresentedAccessToken)
    : IRequest<RegisterResult>, ICustomerCommand;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Customer.EmailMaxLength)
            .Must(BeAnEmailAddress).WithMessage("That does not look like an email address.");
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(Customer.NameMaxLength);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(Customer.NameMaxLength);
        RuleFor(c => c.Mobile).Must(m => PhoneNumber.TryNormaliseMobile(m, out _))
            .WithMessage("Enter a Pakistani mobile number such as 0300 1234567.");
        RuleFor(c => c.Password).NotNull();
        RuleFor(c => c.ClientIp).MaximumLength(CustomerSession.ClientIpMaxLength);
    }

    private static bool BeAnEmailAddress(string? email)
    {
        try
        {
            _ = Customer.NormaliseEmail(email ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class RegisterCommandHandler(ICustomerDbContext dbContext, IPasswordHasher hasher, CustomerSessionIssuer sessions)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var unmet = PasswordRule.Check(request.Password.Reveal()).Where(c => !c.Met).Select(c => c.Code).ToList();
        if (unmet.Count > 0)
        {
            return new RegisterResult(RegisterStatus.PasswordRejected, unmet, null);
        }

        var email = Customer.NormaliseEmail(request.Email);
        if (await dbContext.Customers.AnyAsync(c => c.Email == email, cancellationToken))
        {
            return RegisterResult.Taken;
        }

        var customer = Customer.Register(email, request.FirstName, request.LastName, request.Mobile, hasher.Hash(request.Password));
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken); // the session needs the new id

        var session = await sessions.IssueAsync(customer, request.KeepSignedIn, request.PresentedAccessToken, request.ClientIp, request.UserAgent, cancellationToken);
        return new RegisterResult(RegisterStatus.Registered, [], session);
    }
}

public enum SignInStatus
{
    SignedIn = 1,

    /// <summary>One answer for an unknown email, a wrong password and an account with no password (ACT-5).</summary>
    Failed = 2,

    /// <summary>A delay is running for this email and address; answer 429 with <see cref="SignInResult.RetryAfterSeconds"/> (ACT-9).</summary>
    Throttled = 3,

    /// <summary>The throttle needs the CAPTCHA and the request carried none, or a wrong one (ACT-9).</summary>
    CaptchaRequired = 4,
}

public sealed record SignInResult(SignInStatus Status, int? RetryAfterSeconds, StartedSession? Session)
{
    public static readonly SignInResult Failure = new(SignInStatus.Failed, null, null);

    public static readonly SignInResult NeedsCaptcha = new(SignInStatus.CaptchaRequired, null, null);

    public static SignInResult Throttle(TimeSpan wait) => new(SignInStatus.Throttled, (int)Math.Ceiling(wait.TotalSeconds), null);
}

/// <summary>
/// Email and password sign-in (ACT-4, ACT-5, ACT-9 to ACT-12). <paramref name="ClientIp"/> is the real visitor address, worked out by the
/// caller from trusted forwarded headers only. <paramref name="PresentedAccessToken"/> is the session cookie that came with the request; it
/// is revoked when a session is issued (ACT-11).
/// </summary>
public sealed record SignInCommand(
    string Email, SecretString Password, bool KeepSignedIn, string? CaptchaToken, string ClientIp, string? PresentedAccessToken, string? UserAgent)
    : IRequest<SignInResult>, ICustomerCommand;

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        // The email and password are not validated further: a bad one is an ordinary failed attempt, not a different answer (ACT-5).
        RuleFor(c => c.Email).NotNull();
        RuleFor(c => c.Password).NotNull();
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(CustomerSession.ClientIpMaxLength);
    }
}

public sealed class SignInCommandHandler(
    ICustomerDbContext dbContext,
    IPasswordHasher hasher,
    SignInDummyHash dummyHash,
    ICaptchaVerifier captcha,
    SignInGate gate,
    CustomerSessionIssuer sessions,
    TimeProvider clock,
    ILoggerFactory loggers) : IRequestHandler<SignInCommand, SignInResult>
{
    private readonly ILogger _security = loggers.CreateLogger(SecurityEvents.Category);

    public async Task<SignInResult> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var typed = AttemptEmails.Sanitise(request.Email);

        var decision = await gate.CheckAsync(typed, request.ClientIp, now, cancellationToken);
        if (decision.Wait is { } remaining)
        {
            Record(typed, request.ClientIp, LoginOutcome.Throttled, null, now);
            _security.SignInThrottled((int)Math.Ceiling(remaining.TotalSeconds), request.ClientIp);
            return SignInResult.Throttle(remaining);
        }

        if (decision.CaptchaRequired && !await captcha.VerifyAsync(request.CaptchaToken, request.ClientIp, cancellationToken))
        {
            Record(typed, request.ClientIp, LoginOutcome.CaptchaFailed, null, now);
            return SignInResult.NeedsCaptcha;
        }

        var customer = await FindAsync(typed, cancellationToken);

        // Always verify against something, so the time spent does not say whether the email exists.
        var passwordMatches = hasher.Verify(request.Password, customer?.PasswordHash ?? dummyHash.Value);
        if (customer is null || !customer.HasPassword || !passwordMatches)
        {
            Record(typed, request.ClientIp, LoginOutcome.BadCredentials, customer?.Id, now);
            _security.SignInFailed("bad_credentials", customer?.Id ?? 0, request.ClientIp);
            return SignInResult.Failure;
        }

        Record(typed, request.ClientIp, LoginOutcome.Ok, customer.Id, now);
        if (hasher.NeedsRehash(customer.PasswordHash!))
        {
            customer.SetPasswordHash(hasher.Hash(request.Password));
        }

        customer.RecordSignIn(now);
        var session = await sessions.IssueAsync(customer, request.KeepSignedIn, request.PresentedAccessToken, request.ClientIp, request.UserAgent, cancellationToken);
        return new SignInResult(SignInStatus.SignedIn, null, session);
    }

    private void Record(string typed, string clientIp, LoginOutcome outcome, long? customerId, DateTimeOffset now) =>
        dbContext.LoginAttempts.Add(LoginAttempt.Record(typed, clientIp, outcome, now, customerId));

    private async Task<Customer?> FindAsync(string typed, CancellationToken cancellationToken)
    {
        string email;
        try
        {
            email = Customer.NormaliseEmail(typed);
        }
        catch (ArgumentException)
        {
            return null; // not even a possible email address
        }

        return await dbContext.Customers.FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
    }
}

/// <summary><c>Session</c> is set only when the outcome is <see cref="RefreshOutcome.Rotated"/>.</summary>
public sealed record RefreshSessionResult(RefreshOutcome Outcome, StartedSession? Session);

/// <summary>Exchanges the refresh cookie for a new pair (ACT-12). Reuse of an old value revokes the session.</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<RefreshSessionResult>, ICustomerCommand;

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(CustomerSessionIssuer.MaxPresentedLength);
    }
}

/// <summary>
/// The rotation is a conditional update: the row carries a PostgreSQL <c>xmin</c> concurrency token, so the UPDATE only matches if nobody
/// changed the session since it was read. Of several requests with the same cookie one wins; the others find the row changed and answer
/// <see cref="RefreshOutcome.ConcurrentRefresh"/>, which never revokes (the grace is in <see cref="CustomerSession.Rotate"/>).
/// </summary>
public sealed class RefreshSessionCommandHandler(ICustomerDbContext dbContext, TimeProvider clock, ILoggerFactory loggers)
    : IRequestHandler<RefreshSessionCommand, RefreshSessionResult>
{
    public async Task<RefreshSessionResult> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var hash = CustomerTokens.Hash(request.RefreshToken);
        var session = await dbContext.Sessions.FirstOrDefaultAsync(
            s => s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash, cancellationToken);
        if (session is null)
        {
            return new RefreshSessionResult(RefreshOutcome.Unusable, null);
        }

        var result = session.Rotate(request.RefreshToken, clock.GetUtcNow());
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

        if (result.Outcome == RefreshOutcome.ReuseDetected)
        {
            loggers.CreateLogger(SecurityEvents.Category).RefreshReuse(session.Id, session.CustomerId);
            return new RefreshSessionResult(RefreshOutcome.ReuseDetected, null);
        }

        return new RefreshSessionResult(
            RefreshOutcome.Rotated,
            new StartedSession(session.Id, session.CustomerId, result.Tokens!, session.AccessExpiresAt, session.RefreshExpiresAt));
    }
}

/// <summary>Ends the session the access cookie belongs to. An unknown or empty value changes nothing and is not an error (ACT-12).</summary>
public sealed record SignOutCommand(string? AccessToken) : IRequest, ICustomerCommand;

public sealed class SignOutCommandHandler(ICustomerDbContext dbContext, TimeProvider clock) : IRequestHandler<SignOutCommand>
{
    public async Task Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.AccessToken) || request.AccessToken.Length > CustomerSessionIssuer.MaxPresentedLength)
        {
            return;
        }

        var hash = CustomerTokens.Hash(request.AccessToken);
        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.AccessTokenHash == hash, cancellationToken);
        session?.Revoke(CustomerSessionRevokeReason.SignOut, clock.GetUtcNow());
    }
}
