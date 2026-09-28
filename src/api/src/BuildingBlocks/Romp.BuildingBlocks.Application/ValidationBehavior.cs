using FluentValidation;
using MediatR;

namespace Romp.BuildingBlocks.Application;

/// <summary>
/// MediatR pipeline behaviour (Chain of Responsibility / Decorator around the handler): runs
/// every registered <see cref="IValidator{T}"/> for the incoming request before the handler ever
/// runs, and throws <see cref="ValidationException"/> - never touching the database - if any
/// fail (SCRUM-171, AC-15). A request with no registered validator passes straight through.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var validationContext = new ValidationContext<TRequest>(request);

            var failures = (await Task.WhenAll(
                    validators.Select(validator => validator.ValidateAsync(validationContext, cancellationToken))))
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToList();

            if (failures.Count > 0)
            {
                var errors = failures
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).ToArray());

                throw new ValidationException(errors);
            }
        }

        return await next(cancellationToken);
    }
}
