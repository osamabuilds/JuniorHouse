using FluentValidation;

namespace Romp.Modules.Production.Application.ProductionRuns;

internal static class ActingPersonRules
{
    /// <summary>AC-35 / D13: the acting person's name is required (free text, trimmed, 2-100 characters) until real authentication exists (S8).</summary>
    public static IRuleBuilderOptions<T, string> ActingPerson<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => name is not null && name.Trim().Length is >= 2 and <= 100)
            .WithMessage("Enter the name of the person recording this (2 to 100 characters).");
}
