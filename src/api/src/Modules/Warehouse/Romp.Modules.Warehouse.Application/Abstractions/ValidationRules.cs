using FluentValidation;

namespace Romp.Modules.Warehouse.Application.Abstractions;

internal static class ValidationRules
{
    public const int MaxNoteLength = 200;
    public const int MaxSearchLength = 50;

    /// <summary>
    /// The acting person's name is required (free text, trimmed, 2-100 characters, no control characters) until real
    /// authentication exists (S8). Control characters are refused so the name cannot forge a line in a log (ADR 0011).
    /// </summary>
    public static IRuleBuilderOptions<T, string> ActingPerson<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => name is not null && name.Trim().Length is >= 2 and <= 100 && !HasControlCharacters(name))
            .WithMessage("Enter the name of the person doing this (2 to 100 characters).");

    /// <summary>An optional free-text note: at most <see cref="MaxNoteLength"/> characters and no control characters.</summary>
    public static IRuleBuilderOptions<T, string?> OptionalNote<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(note => note is null || (note.Length <= MaxNoteLength && !HasControlCharacters(note)))
            .WithMessage($"A note can be at most {MaxNoteLength} characters, without control characters.");

    public static bool HasControlCharacters(string text) => text.Any(char.IsControl);
}
