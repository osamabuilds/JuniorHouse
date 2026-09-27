using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Romp.BuildingBlocks.Application;

/// <summary>
/// MediatR pipeline behaviour that logs every request's name, outcome and elapsed time. Runs
/// before <see cref="ValidationBehavior{TRequest,TResponse}"/> in the chain (registration order in
/// Program.cs), so a rejected-by-validation request is still logged, just as a warning rather than
/// an information/error.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        LogHandling(requestName);

        try
        {
            var response = await next(cancellationToken);
            LogHandled(requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (ValidationException ex)
        {
            LogRejectedByValidation(requestName, stopwatch.ElapsedMilliseconds, string.Join("; ", ex.Errors.Keys));
            throw;
        }
        catch (Exception ex)
        {
            LogFailed(ex, requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private partial void LogHandling(string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMilliseconds}ms")]
    private partial void LogHandled(string requestName, long elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} rejected by validation in {ElapsedMilliseconds}ms: {Errors}")]
    private partial void LogRejectedByValidation(string requestName, long elapsedMilliseconds, string errors);

    [LoggerMessage(Level = LogLevel.Error, Message = "{RequestName} failed after {ElapsedMilliseconds}ms")]
    private partial void LogFailed(Exception exception, string requestName, long elapsedMilliseconds);
}
