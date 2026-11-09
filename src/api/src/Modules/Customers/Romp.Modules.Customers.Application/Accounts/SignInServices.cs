using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Accounts;

/// <summary>A session just started: the cookie values (shown once) and when each part ends.</summary>
public sealed record StartedSession(
    long SessionId, long CustomerId, IssuedTokens Tokens, DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt,
    string FirstName = "", bool EmailVerified = false);

/// <summary>What the throttle says about the next attempt: how long it must still wait, and whether it needs the CAPTCHA.</summary>
public sealed record SignInGateDecision(TimeSpan? Wait, bool CaptchaRequired);

/// <summary>
/// Reads the attempt log to decide how an attempt is treated (ACT-9, SEC-02): progressive delay and CAPTCHA, never a lock. Two
/// independent counts, each over the last <see cref="Window"/>: the failures since the last success for this email from this address,
/// and the failures from this address overall. The stricter decision wins. Throttled attempts are not counted, so hammering during a
/// delay does not lengthen it. Unknown emails are treated exactly like known ones.
/// </summary>
public sealed class SignInGate(ICustomerDbContext dbContext)
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>How many recent attempts of an email+address pair are read; the delay reaches its cap well before this many failures.</summary>
    private const int PairLookback = 12;

    public async Task<SignInGateDecision> CheckAsync(string email, string clientIp, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now - Window;

        var pair = await dbContext.LoginAttempts.AsNoTracking()
            .Where(a => a.AttemptedEmail == email && a.ClientIp == clientIp && a.AttemptedAt >= since && LoginOutcomes.Counted.Contains(a.Outcome))
            .OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id)
            .Select(a => new { a.Outcome, a.AttemptedAt })
            .Take(PairLookback)
            .ToListAsync(cancellationToken);

        var pairFailures = pair.TakeWhile(a => !LoginOutcomes.IsSuccess(a.Outcome)).Count();
        var pairDecision = Decide(pairFailures, pairFailures > 0 ? pair[0].AttemptedAt : null, now);

        var address = await dbContext.LoginAttempts.AsNoTracking()
            .Where(a => a.ClientIp == clientIp && a.AttemptedAt >= since && LoginOutcomes.Failures.Contains(a.Outcome))
            .GroupBy(a => a.ClientIp)
            .Select(g => new { Count = g.Count(), Last = g.Max(a => a.AttemptedAt) })
            .FirstOrDefaultAsync(cancellationToken);
        var addressDecision = address is null ? new SignInGateDecision(null, false) : Decide(address.Count, address.Last, now);

        var wait = pairDecision.Wait > addressDecision.Wait ? pairDecision.Wait : addressDecision.Wait;
        return new SignInGateDecision(wait ?? pairDecision.Wait, pairDecision.CaptchaRequired || addressDecision.CaptchaRequired);
    }

    private static SignInGateDecision Decide(int failures, DateTimeOffset? lastFailureAt, DateTimeOffset now)
    {
        var decision = SignInThrottle.Evaluate(failures);
        if (lastFailureAt is null || decision.Delay == TimeSpan.Zero)
        {
            return new SignInGateDecision(null, decision.CaptchaRequired);
        }

        var remaining = lastFailureAt.Value + decision.Delay - now;
        return new SignInGateDecision(remaining > TimeSpan.Zero ? remaining : null, decision.CaptchaRequired);
    }
}

/// <summary>
/// The one place a session is started, so every path gets the same rules: a session cookie sent with the request is revoked first and its
/// value never reused (session fixation, SEC-04), the new session always has new values, and a person never holds more than
/// <see cref="CustomerSession.MaxPerCustomer"/> sessions (the oldest are revoked).
/// </summary>
public sealed class CustomerSessionIssuer(ICustomerDbContext dbContext, TimeProvider clock)
{
    /// <summary>The longest text that can be a token; longer values are not looked up.</summary>
    public const int MaxPresentedLength = 128;

    public async Task<StartedSession> IssueAsync(
        Customer customer, bool keepSignedIn, string? presentedAccessToken, string? clientIp, string? userAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var now = clock.GetUtcNow();
        await RevokePresentedAsync(presentedAccessToken, now, cancellationToken);

        var open = await dbContext.Sessions
            .Where(s => s.CustomerId == customer.Id && s.RevokedAt == null && s.AbsoluteExpiresAt > now && s.RefreshExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var oldest in CustomerSession.SelectToEvict(open, now))
        {
            oldest.Revoke(CustomerSessionRevokeReason.SessionLimit, now);
        }

        var (session, tokens) = CustomerSession.Start(customer.Id, now, CustomerSessionLifetimes.For(keepSignedIn), clientIp, userAgent);
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken); // the caller needs the new session id

        return new StartedSession(session.Id, customer.Id, tokens, session.AccessExpiresAt, session.RefreshExpiresAt, customer.FirstName, customer.EmailIsVerified);
    }

    /// <summary>Ends every open session of the customer except <paramref name="exceptSessionId"/> (ACT-13: password change or reset).</summary>
    public async Task RevokeAllAsync(long customerId, long? exceptSessionId, CustomerSessionRevokeReason reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var open = await dbContext.Sessions.Where(s => s.CustomerId == customerId && s.RevokedAt == null && s.Id != exceptSessionId).ToListAsync(cancellationToken);
        foreach (var session in open)
        {
            session.Revoke(reason, now);
        }
    }

    /// <summary>The id of the customer's own session behind this access cookie, or null (unknown value, or another customer's).</summary>
    public async Task<long?> FindAccessSessionIdAsync(long customerId, string? presentedAccessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(presentedAccessToken) || presentedAccessToken.Length > MaxPresentedLength)
        {
            return null;
        }

        var hash = CustomerTokens.Hash(presentedAccessToken);
        return await dbContext.Sessions.Where(s => s.CustomerId == customerId && s.AccessTokenHash == hash).Select(s => (long?)s.Id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Whatever session the request carried ends, whoever it belonged to; an unknown or oversized value is ignored.</summary>
    private async Task RevokePresentedAsync(string? presented, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(presented) || presented.Length > MaxPresentedLength)
        {
            return;
        }

        var hash = CustomerTokens.Hash(presented);
        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.AccessTokenHash == hash, cancellationToken);
        session?.Revoke(CustomerSessionRevokeReason.NewSignIn, now);
    }
}

/// <summary>What is written to the attempt log as the typed email (ACT-10).</summary>
internal static class AttemptEmails
{
    /// <summary>
    /// Control characters removed, trimmed, lower-cased and truncated. Lower-casing is deliberate: addresses are compared case-insensitively,
    /// so "Ayesha@x.pk" and "ayesha@x.pk" must share one throttle, or capitals would dodge it.
    /// </summary>
    public static string Sanitise(string? typed)
    {
        var cleaned = new string((typed ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim().ToLowerInvariant();
        return cleaned.Length > LoginAttempt.EmailMaxLength ? cleaned[..LoginAttempt.EmailMaxLength] : cleaned;
    }
}

public static class AccountsServiceCollectionExtensions
{
    /// <summary>Registers the sign-in helpers (accounts S5). The password hasher, captcha verifier and clock are registered by the host.</summary>
    public static IServiceCollection AddCustomerAccounts(this IServiceCollection services)
    {
        services.AddScoped<SignInGate>();
        services.AddScoped<CustomerSessionIssuer>();
        services.AddScoped<AccountMail>();
        services.AddScoped<SignedInHooks>();
        services.AddScoped<EmailVerificationIssuer>();
        services.AddSingleton<SignInDummyHash>();
        return services;
    }
}

/// <summary>
/// A real hash of a random value, verified against when the email is unknown, so an unknown email costs the same time as a known one
/// (ACT-5). Built with the current hasher settings when first needed and kept for the life of the process.
/// </summary>
public sealed class SignInDummyHash(IPasswordHasher hasher)
{
    private readonly Lazy<string> _value = new(() => hasher.Hash(new SecretString(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))));

    public string Value => _value.Value;
}
