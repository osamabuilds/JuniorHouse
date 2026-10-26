using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Romp.ArchitectureTests;

/// <summary>
/// docs/specs/warehouse/plan.md and the constitution (section 6): the six Warehouse features depend on each other in ONE direction only,
/// through each callee's public interface. putaway and adjustments use bins and stock-ledger; cycle-counts uses adjustments, bins and
/// stock-ledger; stock-alerts uses only stock-ledger's read interface; bins and stock-ledger know none of the others. A cycle or a
/// back-reference fails the build (G7).
/// </summary>
public sealed class WarehouseFeatureDependencyTests
{
    private const string App = "Romp.Modules.Warehouse.Application";
    private const string Dom = "Romp.Modules.Warehouse.Domain";

    private static readonly System.Reflection.Assembly Application = typeof(Romp.Modules.Warehouse.Application.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Domain = typeof(Romp.Modules.Warehouse.Domain.Grades).Assembly;

    private static readonly string[] Features = ["Bins", "StockLedger", "Putaway", "Adjustments", "CycleCounts", "StockAlerts"];

    /// <summary>For each feature: the features it may use. Everything else is forbidden.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["Bins"] = [],
        ["StockLedger"] = [],
        ["Putaway"] = ["Bins", "StockLedger"],
        ["Adjustments"] = ["Bins", "StockLedger"],
        ["CycleCounts"] = ["Adjustments", "Bins", "StockLedger"],
        ["StockAlerts"] = ["StockLedger"],
    };

    public static TheoryData<string> AllFeatures => new(Features);

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void ApplicationFeatures_DependOnlyOnTheirAllowedFeatures(string feature)
    {
        var forbidden = Features.Where(f => f != feature && !Allowed[feature].Contains(f)).Select(f => $"{App}.{f}").ToArray();

        var result = Types.InAssembly(Application)
            .That().ResideInNamespace($"{App}.{feature}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} must not depend on {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void DomainFeatures_DependOnNoOtherFeature(string feature)
    {
        var forbidden = Features.Where(f => f != feature).Select(f => $"{Dom}.{f}").ToArray();

        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace($"{Dom}.{feature}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} (Domain) must not depend on another feature: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void WarehouseDomain_DependsOnNoFrameworkOrOtherModule()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR", "FluentValidation", "Npgsql",
                "Romp.Modules.Vendor", "Romp.Modules.Catalog", "Romp.Modules.Reference", "Romp.Modules.Quality", "Romp.Modules.Production", "Romp.Modules.Labelling")
            .GetResult();

        Assert.True(result.IsSuccessful, "The Domain must stay pure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void WarehouseApplication_NeverReferencesInfrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn("Romp.Modules.Warehouse.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application depends on its own ports, never on Infrastructure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    [Trait("Spec", "TC-PUT-05")]
    [Trait("Spec", "PUT-16")]
    public void NoOtherModule_ReadsTheWarehouseSchema()
    {
        var modulesRoot = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modulesRoot, file).Replace('\\', '/');
            if (relative.StartsWith("Warehouse/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, "\"WHSE\"|Romp\\.Modules\\.Warehouse\\.(Domain|Application|Infrastructure)"))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, "Only Warehouse may touch WHSE or its internals; other modules use events or Warehouse.Contracts: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "LED-10")]
    public void TheStockRoutes_AreReadOnly_AndNoAdjustmentRouteCanEditOrDelete()
    {
        var infra = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Warehouse", "Romp.Modules.Warehouse.Infrastructure");
        var stock = File.ReadAllText(Path.Combine(infra, "StockLedger", "StockEndpoints.cs"));
        var adjustments = File.ReadAllText(Path.Combine(infra, "Adjustments", "AdjustmentEndpoints.cs"));

        Assert.DoesNotMatch(@"\.Map(Post|Put|Patch|Delete)\(", stock);
        Assert.DoesNotMatch(@"\.Map(Put|Patch|Delete)\(", adjustments);
    }

    [Fact]
    [Trait("Spec", "TC-STK-06")]
    [Trait("Spec", "SEC-08")]
    public void TheWritesThatChangeStockNeedTheInventoryWritePermission()
    {
        var infra = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Warehouse", "Romp.Modules.Warehouse.Infrastructure");

        foreach (var file in new[] { "Adjustments/AdjustmentEndpoints.cs", "StockAlerts/StockAlertInfrastructure.cs", "CycleCounts/CycleCountPorts.cs" })
        {
            Assert.Contains("Permissions.InventoryWrite", File.ReadAllText(Path.Combine(infra, file)), StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (CLAUDE.md) not found.");
    }
}
