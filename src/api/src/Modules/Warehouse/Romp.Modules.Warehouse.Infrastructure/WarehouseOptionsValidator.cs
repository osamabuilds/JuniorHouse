using Microsoft.Extensions.Options;
using Romp.Modules.Warehouse.Application.Abstractions;

namespace Romp.Modules.Warehouse.Infrastructure;

/// <summary>
/// Start-up validation of <c>Warehouse:*</c> (plan G4): a limit or threshold outside its range stops the host from starting.
/// </summary>
public sealed class WarehouseOptionsValidator : IValidateOptions<WarehouseOptions>
{
    private const int MaxUnits = 100_000;

    public ValidateOptionsResult Validate(string? name, WarehouseOptions options)
    {
        var failures = new List<string>();

        if (options.LargeAdjustmentUnits is < 1 or > MaxUnits)
        {
            failures.Add($"Warehouse:LargeAdjustmentUnits must be between 1 and {MaxUnits}.");
        }

        if (options.DefaultLowStockThreshold is < 0 or > MaxUnits)
        {
            failures.Add($"Warehouse:DefaultLowStockThreshold must be between 0 and {MaxUnits}.");
        }

        if (options.MaxLinesPerAcceptedBatch < 1)
        {
            failures.Add("Warehouse:MaxLinesPerAcceptedBatch must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
