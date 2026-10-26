using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>
/// A shopper's account record (accounts, Sprint 10 slice). It holds who the customer is and nothing about how they sign in: credentials
/// arrive with the final authentication sprint, beside this row. The email is stored trimmed and lower-case and is unique.
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
