using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>
/// A single-use, expiring link behind an emailed address (ACT-7, ACT-8, ACT-27, ACT-28). The link value is random and only its hash is stored.
/// It works once, until it expires or until a newer link replaces it.
/// </summary>
public abstract class OneTimeToken : AggregateRoot<long>, IAuditable
{
    protected OneTimeToken()
    {
        TokenHash = [];
        InsrBy = string.Empty;
    }

    public long CustomerId { get; protected set; }

    public byte[] TokenHash { get; protected set; }

    public DateTimeOffset IssuedAt { get; protected set; }

    public DateTimeOffset ExpiresAt { get; protected set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    /// <summary>Uses the link when <paramref name="presented"/> is its value and it is still usable. Returns whether it worked; a failed try changes nothing.</summary>
    public bool Consume(string? presented, DateTimeOffset now)
    {
        if (!IsUsable(now) || !CustomerTokens.Matches(presented, TokenHash))
        {
            return false;
        }

        UsedAt = now;
        return true;
    }

    /// <summary>Retires a link without using it, because a newer one was sent.</summary>
    public void Supersede(DateTimeOffset now) => UsedAt ??= now;

    protected SecretString Start(long customerId, DateTimeOffset now, TimeSpan lifetime)
    {
        var secret = CustomerTokens.Generate();
        CustomerId = customerId;
        TokenHash = CustomerTokens.Hash(secret.Reveal());
        IssuedAt = now;
        ExpiresAt = now + lifetime;
        return secret;
    }
}

/// <summary>CUST.CUST_PWRT: the emailed password-reset link, valid for 60 minutes (Q-ACT-15).</summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: a short-lived token; the reset itself is recorded on the sign-in attempts.")]
public sealed class PasswordResetToken : OneTimeToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    private PasswordResetToken()
    {
    }

    public static (PasswordResetToken Token, SecretString Secret) Issue(long customerId, DateTimeOffset now)
    {
        var token = new PasswordResetToken();
        var secret = token.Start(customerId, now, Lifetime);
        return (token, secret);
    }
}

/// <summary>CUST.CUST_EVRT: the emailed verification link, valid for 24 hours (Q-ACT-15). It proves one address, kept on the row.</summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: a short-lived token; the verified date is on the customer.")]
public sealed class EmailVerificationToken : OneTimeToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>How many new links one customer may ask for in an hour (ACT-28).</summary>
    public const int MaxPerHour = 5;

    private EmailVerificationToken()
    {
        Email = string.Empty;
    }

    public string Email { get; private set; }

    public static (EmailVerificationToken Token, SecretString Secret) Issue(long customerId, string email, DateTimeOffset now)
    {
        var token = new EmailVerificationToken { Email = Customer.NormaliseEmail(email) };
        var secret = token.Start(customerId, now, Lifetime);
        return (token, secret);
    }
}
