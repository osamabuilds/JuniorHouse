using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

/// <summary>The customer's own account. No route takes a customer id: the caller is the customer (ACT-18, SEC-05).</summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapSecuredGroup("/api/customers/me", Policies.Customer).WithTags("Customers").AddEndpointFilter<CustomerNoStoreFilter>();

        me.MapGet("", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetCurrentCustomerQuery(), cancellationToken)));
    }
}
