using Microsoft.Extensions.Options;

namespace Romp.Modules.Storefront.Infrastructure;

/// <summary>
/// Start-up validation of <c>Storefront:*</c> (published-catalog task 20; plan: image limits validated at start-up). A value outside its range
/// stops the host from starting, instead of surfacing later as failed derivatives or a reconcile loop that never sleeps.
/// </summary>
public sealed class StorefrontOptionsValidator : IValidateOptions<StorefrontOptions>
{
    internal const int MaxUnits = 100_000;
    internal const int MinImageWidth = 16;
    internal const int MaxImageWidth = 4096;
    internal const int MaxImageWidthCount = 8;
    internal const long MaxPixelCap = 200_000_000;
    internal const int MaxAttemptCap = 20;

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaxPollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan MinReconcileInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxJobInterval = TimeSpan.FromDays(7);

    public ValidateOptionsResult Validate(string? name, StorefrontOptions options)
    {
        var failures = new List<string>();

        if (options.LowStockThreshold is < 0 or > MaxUnits)
        {
            failures.Add($"Storefront:LowStockThreshold must be between 0 and {MaxUnits}.");
        }

        ValidateWidths(options.ImageWidths, failures);

        if (options.MaxImagePixels is < 1 or > MaxPixelCap)
        {
            failures.Add($"Storefront:MaxImagePixels must be between 1 and {MaxPixelCap}.");
        }

        if (options.NewArrivalsDays is < 1 or > 365)
        {
            failures.Add("Storefront:NewArrivalsDays must be between 1 and 365.");
        }

        if (options.ImageQuality is < 1 or > 100)
        {
            failures.Add("Storefront:ImageQuality must be between 1 and 100.");
        }

        if (options.MaxDerivativeAttempts is < 1 or > MaxAttemptCap)
        {
            failures.Add($"Storefront:MaxDerivativeAttempts must be between 1 and {MaxAttemptCap}.");
        }

        RequireBetween(failures, nameof(options.DerivativeRetryDelay), options.DerivativeRetryDelay, TimeSpan.FromSeconds(1), MaxRetryDelay);
        RequireBetween(failures, nameof(options.DerivativeCacheLifetime), options.DerivativeCacheLifetime, TimeSpan.FromHours(1), TimeSpan.FromDays(365));
        RequireBetween(failures, nameof(options.DerivativeSweepInterval), options.DerivativeSweepInterval, TimeSpan.FromMinutes(1), MaxJobInterval);
        RequireBetween(failures, nameof(options.ImageWorkerPollInterval), options.ImageWorkerPollInterval, TimeSpan.FromSeconds(1), MaxPollInterval);
        RequireBetween(failures, nameof(options.ReconcileInterval), options.ReconcileInterval, MinReconcileInterval, MaxJobInterval);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateWidths(int[]? widths, List<string> failures)
    {
        if (widths is null || widths.Length == 0)
        {
            failures.Add("Storefront:ImageWidths must list at least one width.");
            return;
        }

        if (widths.Length > MaxImageWidthCount)
        {
            failures.Add($"Storefront:ImageWidths may list at most {MaxImageWidthCount} widths.");
        }

        if (widths.Any(w => w is < MinImageWidth or > MaxImageWidth))
        {
            failures.Add($"Storefront:ImageWidths: every width must be between {MinImageWidth} and {MaxImageWidth} pixels.");
        }

        if (widths.Distinct().Count() != widths.Length)
        {
            failures.Add("Storefront:ImageWidths must not repeat a width.");
        }
    }

    private static void RequireBetween(List<string> failures, string setting, TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min || value > max)
        {
            failures.Add($"Storefront:{setting} must be between {min} and {max}.");
        }
    }
}
