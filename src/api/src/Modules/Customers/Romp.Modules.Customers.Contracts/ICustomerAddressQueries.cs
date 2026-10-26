namespace Romp.Modules.Customers.Contracts;

/// <summary>A saved address as another module (checkout) may see it. Province and city are Reference ids; names come from Reference.</summary>
public sealed record CustomerAddressSummary(
    long Id, string RecipientName, string Phone, short ProvinceId, short CityId, string LineOne, string? LineTwo, string? Postcode, bool IsDefault);

/// <summary>
/// The only way another module reads a customer's saved addresses (FR-40, ADR 0002). Both calls are scoped to the signed-in customer and take
/// no customer id (SEC-05): a guest gets nothing, and an id that belongs to someone else is the same as a missing one.
/// </summary>
public interface ICustomerAddressQueries
{
    /// <summary>The caller's addresses, default first then newest. A book holds at most a few dozen, so this is not paged.</summary>
    Task<IReadOnlyList<CustomerAddressSummary>> GetMyAddressesAsync(CancellationToken cancellationToken);

    Task<CustomerAddressSummary?> GetMyAddressAsync(long addressId, CancellationToken cancellationToken);
}
