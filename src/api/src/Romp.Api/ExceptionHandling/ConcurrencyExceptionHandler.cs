using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Romp.Api.ExceptionHandling;

/// <summary>
/// Maps two race outcomes to 409 Conflict instead of a 500 (spec AC-4, AC-43): an optimistic-concurrency failure (another
/// user changed the same row first) and a unique-constraint violation (another request created the same thing first).
/// The database constraint is the last line of defence; the user just needs to reload and look again.
/// </summary>
public sealed class ConcurrencyExceptionHandler : IExceptionHandler
{
    private const string UniqueViolation = "23505";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        string? detail = exception switch
        {
            DbUpdateConcurrencyException => "Someone else changed this record while you were working. Reload it and try again.",
            DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } } =>
                "That record already exists (another request created it first). Reload and check before trying again.",
            _ => null,
        };

        if (detail is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "The request conflicts with the resource's current state.", Detail = detail },
            cancellationToken);
        return true;
    }
}
