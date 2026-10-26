using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;

namespace Romp.Modules.Customers.Application.Accounts;

/// <summary>What the account shell needs to greet the customer. No address, no phone: other screens fetch their own data.</summary>
public sealed record CurrentCustomerDto(long Id, string FirstName, string LastName, string Email);

/// <summary>The caller's own account (ACT-18). It has no id parameter: the customer comes from <see cref="ICurrentCustomer"/>.</summary>
public sealed record GetCurrentCustomerQuery : IRequest<CurrentCustomerDto>;

public sealed class GetCurrentCustomerQueryHandler(ICustomerDbContext dbContext, ICurrentCustomer current)
    : IRequestHandler<GetCurrentCustomerQuery, CurrentCustomerDto>
{
    public async Task<CurrentCustomerDto> Handle(GetCurrentCustomerQuery request, CancellationToken cancellationToken)
    {
        var id = current.Require();
        return await dbContext.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CurrentCustomerDto(c.Id, c.FirstName, c.LastName, c.Email))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Customer not found.");
    }
}
