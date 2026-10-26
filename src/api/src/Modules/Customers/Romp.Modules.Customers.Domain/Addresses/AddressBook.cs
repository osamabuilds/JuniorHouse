using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Addresses;

/// <summary>
/// The rules of one customer's address book as a pure domain service: the first address becomes the default, a book never exceeds its limit,
/// exactly one address is the default while the book is not empty, and removing the default promotes the most recent remaining address (the
/// highest id). Callers pass the customer's whole book (at most the limit, so loading it is cheap) and persist the changed rows in one
/// transaction (ADD-2, ADD-6).
/// </summary>
public static class AddressBook
{
    public static void Add(IReadOnlyCollection<CustomerAddress> book, CustomerAddress added, int maxAddresses)
    {
        if (book.Count >= maxAddresses)
        {
            throw new DomainException($"You can save up to {maxAddresses} addresses. Remove one to add another.");
        }

        added.SetDefault(book.Count == 0);
    }

    public static void MakeDefault(IReadOnlyCollection<CustomerAddress> book, CustomerAddress target)
    {
        foreach (var address in book)
        {
            address.SetDefault(ReferenceEquals(address, target));
        }
    }

    /// <summary>Removes <paramref name="removed"/> from the rules' point of view and returns the address promoted to default, if any.</summary>
    public static CustomerAddress? Remove(IReadOnlyCollection<CustomerAddress> book, CustomerAddress removed)
    {
        if (!removed.IsDefault)
        {
            return null;
        }

        var promoted = book.Where(a => !ReferenceEquals(a, removed)).OrderByDescending(a => a.Id).FirstOrDefault();
        removed.SetDefault(false);
        promoted?.SetDefault(true);
        return promoted;
    }
}
