using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Accounts;

/// <summary>A request that carries a usable access cookie: who and which session. Nothing else is trusted from the cookie.</summary>
public sealed record AuthenticatedCustomer(long CustomerId, long SessionId);

/// <summary>Asks whether an access cookie value belongs to a session that can be used now (ACT-11). Null for anything else.</summary>
public sealed record AuthenticateCustomerSessionQuery(string? AccessToken) : IRequest<AuthenticatedCustomer?>;

public sealed class AuthenticateCustomerSessionQueryHandler(ICustomerDbContext dbContext, TimeProvider clock)
    : IRequestHandler<AuthenticateCustomerSessionQuery, AuthenticatedCustomer?>
{
    public async Task<AuthenticatedCustomer?> Handle(AuthenticateCustomerSessionQuery request, CancellationToken cancellationToken)
    {
        var token = request.AccessToken;
        if (string.IsNullOrEmpty(token) || token.Length > CustomerSessionIssuer.MaxPresentedLength)
        {
            return null;
        }

        var hash = CustomerTokens.Hash(token);
        var session = await dbContext.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.AccessTokenHash == hash, cancellationToken);
        return session is not null && session.AccessTokenMatches(token) && session.AccessIsValid(clock.GetUtcNow())
            ? new AuthenticatedCustomer(session.CustomerId, session.Id)
            : null;
    }
}
