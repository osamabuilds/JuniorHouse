namespace Romp.BuildingBlocks.Application;

/// <summary>
/// Carries field-level validation failures out of <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// A host-level exception handler (Romp.Api) maps this to a 400 <c>ValidationProblemDetails</c>
/// (AC-15: "the specific field-level error is shown"). <see cref="Exception.Message"/> is the
/// actual rule violations, never a generic "one or more errors" line.
/// </summary>
public sealed class ValidationException(IDictionary<string, string[]> errors)
    : Exception(Summarise(errors))
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    /// <summary>All violation messages, in order, joined into one readable sentence block.</summary>
    public static string Summarise(IDictionary<string, string[]> errors) =>
        string.Join(" ", errors.Values.SelectMany(messages => messages));
}
