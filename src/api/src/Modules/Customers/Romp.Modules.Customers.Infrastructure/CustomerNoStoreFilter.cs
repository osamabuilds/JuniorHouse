using Microsoft.AspNetCore.Http;

namespace Romp.Modules.Customers.Infrastructure;

/// <summary>Customer data is personal: no shared cache or browser history may keep a response (SEC-25, NFR-26).</summary>
internal sealed class CustomerNoStoreFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }
}
