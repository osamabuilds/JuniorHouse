namespace Romp.BuildingBlocks.Application;

/// <summary>
/// Carries field-level validation failures out of <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// A host-level exception handler (Romp.Api) maps this to a 400 <c>ValidationProblemDetails</c>
/// (AC-15: "the specific field-level error is shown").
/// </summary>
public sealed class ValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
