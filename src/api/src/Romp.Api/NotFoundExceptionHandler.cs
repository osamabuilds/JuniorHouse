using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Romp.Api;

/// <summary>
/// Maps <see cref="KeyNotFoundException"/> - what every handler throws for "that id doesn't exist"
/// (an unknown PO, a file that belongs to a different PO, ...) - to a 404 with the handler's own
/// message, instead of letting it surface as a 500 that looks like a server fault.
/// </summary>
public sealed class NotFoundExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not KeyNotFoundException notFound)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = notFound.Message,
            },
            cancellationToken);

        return true;
    }
}
