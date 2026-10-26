using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

/// <summary>Customer data is personal: no shared cache or browser history may keep a response (SEC-25, NFR-26).</summary>
internal sealed class CustomerNoStoreFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }
}

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
