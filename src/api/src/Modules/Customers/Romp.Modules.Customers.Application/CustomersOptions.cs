namespace Romp.Modules.Customers.Application;

/// <summary>The <c>Customers</c> configuration section (module plan, "Policies, configuration and the web edge"). Validated at start-up.</summary>
public sealed class CustomersOptions
{
    public const string SectionName = "Customers";

    /// <summary>How many saved addresses one customer may keep (ADD-6, Q-ADD-3 default).</summary>
    public int MaxAddresses { get; set; } = 10;

    public static bool IsValid(CustomersOptions options) => options.MaxAddresses is >= 1 and <= 50;
}
