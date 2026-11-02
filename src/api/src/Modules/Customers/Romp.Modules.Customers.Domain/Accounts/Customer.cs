using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>
/// A shopper's account record (accounts). It holds who the customer is, the hash of their password (never the password), whether the email
/// is verified, and the mobile number, which is saved but not verified for now (ACT-29). The email is stored trimmed and lower-case and is unique.
/// </summary>
public sealed class Customer : AggregateRoot<long>, IAuditable
{
    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 80;
    public const int PhoneMaxLength = 20;

    private Customer()
    {
        Email = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        InsrBy = string.Empty;
    }

    public string Email { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>The Argon2id hash of the password; null for an account that has none (the development stand-in).</summary>
    public string? PasswordHash { get; private set; }

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    /// <summary>When the current email was proved by its link (ACT-27); null while unverified. Nothing is blocked while it is null (ACT-31).</summary>
    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public bool EmailIsVerified => EmailVerifiedAt is not null;

    public DateTimeOffset? LastSignedInAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static Customer Create(string email, string firstName, string lastName, string? phone = null)
    {
        var normalised = NormaliseEmail(email);
        return new Customer
        {
            Email = normalised,
            FirstName = Required(firstName, NameMaxLength, nameof(firstName)),
            LastName = Required(lastName, NameMaxLength, nameof(lastName)),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : Required(phone, PhoneMaxLength, nameof(phone)),
        };
    }

    /// <summary>
    /// A customer who registered with email and password (ACT-1). The mobile must be a Pakistani mobile and is stored in one form, unverified
    /// (ACT-29); the email starts unverified (ACT-27).
    /// </summary>
    public static Customer Register(string email, string firstName, string lastName, string? mobile, string passwordHash)
    {
        if (!PhoneNumber.TryNormaliseMobile(mobile, out var normalisedMobile))
        {
            throw new ArgumentException("Enter a Pakistani mobile number such as 0300 1234567.", nameof(mobile));
        }

        var customer = Create(email, firstName, lastName, normalisedMobile);
        customer.SetPasswordHash(passwordHash);
        return customer;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    /// <summary>Marks the email verified when <paramref name="verifiedEmail"/> is the address on the account now. Returns false for any other address.</summary>
    public bool MarkEmailVerified(string verifiedEmail, DateTimeOffset now)
    {
        if (!string.Equals(NormaliseEmail(verifiedEmail), Email, StringComparison.Ordinal))
        {
            return false;
        }

        EmailVerifiedAt ??= now;
        return true;
    }

    /// <summary>Changes the email; a different address starts unverified (ACT-28). The same address keeps its verified date.</summary>
    public void ChangeEmail(string newEmail)
    {
        var normalised = NormaliseEmail(newEmail);
        if (string.Equals(normalised, Email, StringComparison.Ordinal))
        {
            return;
        }

        Email = normalised;
        EmailVerifiedAt = null;
    }

    public void RecordSignIn(DateTimeOffset now) => LastSignedInAt = now;

    /// <summary>Trims and lower-cases an address and checks its shape. Used for every lookup by email so the same person is always the same key.</summary>
    public static string NormaliseEmail(string email)
    {
        var value = (email ?? string.Empty).Trim().ToLowerInvariant();
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (value.Length is < 3 or > EmailMaxLength || at < 1 || at != value.LastIndexOf('@') || at == value.Length - 1 || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new ArgumentException("That does not look like an email address.", nameof(email));
        }

        return value;
    }

    private static string Required(string value, int max, string name)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > max || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException($"{name} must be 1 to {max} characters.", name);
        }

        return trimmed;
    }
}
