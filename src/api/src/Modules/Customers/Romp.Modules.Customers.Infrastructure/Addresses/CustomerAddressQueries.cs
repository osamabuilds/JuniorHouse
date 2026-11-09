using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Infrastructure.Addresses;

/// <summary>The Customers side of <see cref="ICustomerAddressQueries"/>: every statement filters by the signed-in customer.</summary>
public sealed class CustomerAddressQueries(CustDbContext dbContext, ICurrentCustomer current) : ICustomerAddressQueries
{
    public async Task<IReadOnlyList<CustomerAddressSummary>> GetMyAddressesAsync(CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return [];
        }

        return await dbContext.Addresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.Id)
            .Select(Summary)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerAddressSummary?> GetMyAddressAsync(long addressId, CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return null;
        }

        return await dbContext.Addresses.AsNoTracking()
            .Where(a => a.Id == addressId && a.CustomerId == customerId)
            .Select(Summary)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static readonly System.Linq.Expressions.Expression<Func<CustomerAddress, CustomerAddressSummary>> Summary = a =>
        new CustomerAddressSummary(a.Id, a.RecipientName, a.Phone, a.ProvinceId, a.CityId, a.LineOne, a.LineTwo, a.Postcode, a.IsDefault);
}
