using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Infrastructure;

/// <summary>
/// Start-up validation of <c>Labelling:*</c> (VAR-11, ADR 0009, ADR 0011): a bad prefix, a test prefix in Production, or fake printers
/// in Production stop the host from starting, so dangerous configuration can never reach real labels.
/// </summary>
public sealed class LabellingOptionsValidator(IHostEnvironment environment) : IValidateOptions<LabellingOptions>
{
    public ValidateOptionsResult Validate(string? name, LabellingOptions options)
    {
        var failures = new List<string>();

        if (!CompanyPrefix.TryParse(options.CompanyPrefix, out var prefix, out var error))
        {
            failures.Add($"Labelling:CompanyPrefix: {error}");
        }
        else if (prefix!.IsTestPrefix && environment.IsProduction())
        {
            failures.Add("Labelling:CompanyPrefix is a GS1 test prefix (020-029); production must use the company's real prefix.");
        }

        if (options.AllowFakePrinters && environment.IsProduction())
        {
            failures.Add("Labelling:AllowFakePrinters must be false in Production: fake printers would report tags as printed that were not.");
        }

        if (options.SparePercent is < 0 or > 50)
        {
            failures.Add("Labelling:SparePercent must be between 0 and 50.");
        }

        if (options.MaxTagsPerJob < 1 || options.MaxTagsPerJob > 99_999)
        {
            failures.Add("Labelling:MaxTagsPerJob must be between 1 and 99,999.");
        }

        if (options.MaxQueuedJobsPerPrinter < 1 || options.MaxLinesPerAcceptedBatch < 1 || options.MaxAttempts < 1 || options.StuckJobMinutes < 1)
        {
            failures.Add("Labelling limits (MaxQueuedJobsPerPrinter, MaxLinesPerAcceptedBatch, MaxAttempts, StuckJobMinutes) must be positive.");
        }

        if (options.BackoffSeconds.Any(s => s < 0))
        {
            failures.Add("Labelling:BackoffSeconds cannot contain negative values.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
