using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Addresses;

/// <summary>
/// One saved delivery address in a customer's book (ADD-1). The customer id never changes after creation and is always set from the signed-in
/// customer, never from a request (SEC-05). Province and city are plain ids into the Reference lookups (no cross-schema key); whether the city
/// belongs to the province is checked by the application layer against the Reference contract (ADD-4). The default flag is managed by
/// <see cref="AddressBook"/> so exactly one address of a non-empty book is the default (ADD-2).
/// </summary>
public sealed class CustomerAddress : Entity<long>, IAuditable
{
    public const int RecipientMinLength = 2;
    public const int RecipientMaxLength = 80;
    public const int LineOneMinLength = 5;
    public const int LineMaxLength = 150;
    public const int PostcodeMaxLength = 10;

    private CustomerAddress()
    {
        RecipientName = string.Empty;
        Phone = string.Empty;
        LineOne = string.Empty;
        InsrBy = string.Empty;
    }

    public long CustomerId { get; private set; }

    public string RecipientName { get; private set; }

    public string Phone { get; private set; }

    public short ProvinceId { get; private set; }

    public short CityId { get; private set; }

    public string LineOne { get; private set; }

    public string? LineTwo { get; private set; }

    public string? Postcode { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static CustomerAddress Create(
        long customerId, string recipientName, string phone, short provinceId, short cityId, string lineOne, string? lineTwo, string? postcode)
    {
        var address = new CustomerAddress { CustomerId = customerId };
        address.Apply(recipientName, phone, provinceId, cityId, lineOne, lineTwo, postcode);
        return address;
    }

    public void Update(string recipientName, string phone, short provinceId, short cityId, string lineOne, string? lineTwo, string? postcode) =>
        Apply(recipientName, phone, provinceId, cityId, lineOne, lineTwo, postcode);

    internal void SetDefault(bool isDefault) => IsDefault = isDefault;

    private void Apply(string recipientName, string phone, short provinceId, short cityId, string lineOne, string? lineTwo, string? postcode)
    {
        var recipient = Text(recipientName, RecipientMinLength, RecipientMaxLength, nameof(recipientName));
        var line1 = Text(lineOne, LineOneMinLength, LineMaxLength, nameof(lineOne));
        var line2 = string.IsNullOrWhiteSpace(lineTwo) ? null : Text(lineTwo, 1, LineMaxLength, nameof(lineTwo));
        var code = string.IsNullOrWhiteSpace(postcode) ? null : CleanPostcode(postcode);

        RecipientName = recipient;
        Phone = PhoneNumber.Normalise(phone);
        ProvinceId = provinceId;
        CityId = cityId;
        LineOne = line1;
        LineTwo = line2;
        Postcode = code;
    }

    private static string Text(string? value, int min, int max, string name)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length < min || trimmed.Length > max || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException($"{name} must be {min} to {max} characters.", name);
        }

        return trimmed;
    }

    private static string CleanPostcode(string postcode)
    {
        var trimmed = postcode.Trim();
        if (trimmed.Length > PostcodeMaxLength || !trimmed.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException($"The postcode must be letters and digits, up to {PostcodeMaxLength} characters.", nameof(postcode));
        }

        return trimmed;
    }
}
