using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Romp.BuildingBlocks.Application;

namespace Romp.Api;

/// <summary>
/// Maps <see cref="ValidationException"/> - thrown by every module's MediatR ValidationBehavior -
/// to a 400 <see cref="ValidationProblemDetails"/> with per-field errors (SCRUM-171, AC-15).
/// Registered via <c>AddExceptionHandler</c>/<c>UseExceptionHandler</c> so no module or handler
/// needs its own try/catch for this.
/// </summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(
            new ValidationProblemDetails(validationException.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request has invalid details.",
                Detail = ValidationException.Summarise(validationException.Errors),
            },
            cancellationToken);

        return true;
    }
}
