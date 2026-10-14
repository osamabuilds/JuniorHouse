using FluentValidation;

namespace Romp.Modules.Catalog.Application.Abstractions;

internal static class ValidationRules
{
    public const int MaxSearchLength = 50;

    /// <summary>
    /// The acting person's name is required (free text, trimmed, 2-100 characters, no control characters) until real
    /// authentication exists (S8). Control characters are refused so the name cannot forge a line in a log (ADR 0011).
    /// </summary>
    public static IRuleBuilderOptions<T, string> ActingPerson<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => name is not null && name.Trim().Length is >= 2 and <= 100 && !HasControlCharacters(name))
            .WithMessage("Enter the name of the person doing this (2 to 100 characters).");

    public static bool HasControlCharacters(string text) => text.Any(char.IsControl);
}
