using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Romp.ArchitectureTests;

/// <summary>
/// docs/specs/catalog/plan.md (module plan, "One-way dependencies inside Catalog") and the constitution (section 6): the Sprint 7
/// features depend on each other in ONE direction. <c>Products</c> calls the gate readers of <c>ProductMedia</c>,
/// <c>CompositionCare</c> and <c>SizeCharts</c> and the path rules of <c>Urls</c>; those four never call <c>Products</c> (or each
/// other). A cycle or a back-reference fails the build (G6).
/// </summary>
public sealed class CatalogFeatureDependencyTests
{
    private const string App = "Romp.Modules.Catalog.Application";
    private const string Dom = "Romp.Modules.Catalog.Domain";

    private static readonly System.Reflection.Assembly Application = typeof(Romp.Modules.Catalog.Application.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Domain = typeof(Romp.Modules.Catalog.Domain.AssemblyReference).Assembly;

    private static readonly string[] Features = ["Products", "ProductMedia", "CompositionCare", "SizeCharts", "Urls"];

    /// <summary>For each feature: the features it may use. Everything else is forbidden.</summary>
    private static readonly Dictionary<string, string[]> AllowedInApplication = new()
    {
        ["Products"] = ["ProductMedia", "CompositionCare", "SizeCharts", "Urls"],
        ["ProductMedia"] = ["Urls"],
        ["CompositionCare"] = ["Urls"],
        ["SizeCharts"] = [],
        ["Urls"] = [],
    };

    /// <summary>In the Domain only the pure slug rules are shared: Products uses Urls' <c>SlugPolicy</c> in the publish gate.</summary>
    private static readonly Dictionary<string, string[]> AllowedInDomain = new()
    {
        ["Products"] = ["Urls"],
        ["ProductMedia"] = [],
        ["CompositionCare"] = [],
        ["SizeCharts"] = [],
        ["Urls"] = [],
    };

    public static TheoryData<string> AllFeatures => new(Features);

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void ApplicationFeatures_DependOnlyOnTheirAllowedFeatures(string feature)
    {
        var forbidden = Features.Where(f => f != feature && !AllowedInApplication[feature].Contains(f)).Select(f => $"{App}.{f}").ToArray();

        var result = Types.InAssembly(Application)
            .That().ResideInNamespace($"{App}.{feature}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} must not depend on {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void DomainFeatures_DependOnlyOnTheirAllowedFeatures(string feature)
    {
        var forbidden = Features.Where(f => f != feature && !AllowedInDomain[feature].Contains(f)).Select(f => $"{Dom}.{f}").ToArray();

        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace($"{Dom}.{feature}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} (Domain) must not depend on {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CatalogDomain_DependsOnNoFrameworkOrOtherModuleInternals()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR", "FluentValidation", "Npgsql",
                "Romp.Modules.Vendor", "Romp.Modules.Warehouse", "Romp.Modules.Reference", "Romp.Modules.Quality", "Romp.Modules.Production", "Romp.Modules.Labelling")
            .GetResult();

        Assert.True(result.IsSuccessful, "The Domain must stay pure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void CatalogApplication_NeverReferencesInfrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn("Romp.Modules.Catalog.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application depends on its own ports, never on Infrastructure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    [Trait("Spec", "ADR-0014")]
    public void CatalogMayReadLabellingAndReference_OnlyThroughTheirContracts()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny(
                "Romp.Modules.Labelling.Domain", "Romp.Modules.Labelling.Application", "Romp.Modules.Labelling.Infrastructure",
                "Romp.Modules.Reference.Domain", "Romp.Modules.Reference.Application", "Romp.Modules.Reference.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    [Trait("Spec", "ADR-0002")]
    public void TheContractsProjects_DoNotReferenceEachOther()
    {
        var contracts = new[]
        {
            typeof(Romp.Modules.Catalog.Contracts.CatalogEvent).Assembly,
            typeof(Romp.Modules.Labelling.Contracts.LabellingEvent).Assembly,
            typeof(Romp.Modules.Reference.Contracts.IApparelCodeQueries).Assembly,
        };

        foreach (var assembly in contracts)
        {
            var others = contracts.Where(c => c != assembly).Select(c => c.GetName().Name!).ToArray();
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
            Assert.Empty(referenced.Intersect(others));
        }
    }

    [Fact]
    [Trait("Spec", "ADR-0002")]
    public void NoOtherModule_ReadsTheCatalogProductTables()
    {
        var modulesRoot = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modulesRoot, file).Replace('\\', '/');
            if (relative.StartsWith("Catalog/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), "\"CTLG\"|Romp\\.Modules\\.Catalog\\.(Domain|Application|Infrastructure)"))
            {
                offenders.Add(relative);
            }
        }

        // Vendor reads styles through IStyleQueries (Sprint 1); only Catalog's own code names the schema or its internals.
        Assert.True(offenders.Count == 0, "Only Catalog may touch CTLG or its internals; others use events or Catalog.Contracts: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public void EveryCatalogueEndpointGroup_NeedsTheCataloguePermissions()
    {
        var infra = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Catalog", "Romp.Modules.Catalog.Infrastructure");

        foreach (var file in new[]
        {
            "Products/ProductEndpoints.cs", "ProductMedia/ProductMediaEndpoints.cs", "CompositionCare/CompositionCareEndpoints.cs",
            "SizeCharts/SizeChartEndpoints.cs", "Urls/UrlEndpoints.cs",
        })
        {
            var text = File.ReadAllText(Path.Combine(infra, file));
            Assert.Contains("Permissions.CatalogRead", text, StringComparison.Ordinal);
            Assert.Contains("Permissions.CatalogWrite", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".MapGroup(", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Spec", "PRD-7")]
    public void PriceHistory_HasNoUpdateOrDeletePath_InTheApplication()
    {
        var app = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Catalog", "Romp.Modules.Catalog.Application");
        foreach (var file in Directory.GetFiles(app, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            Assert.DoesNotMatch(@"PriceHistory\.(Remove|RemoveRange|Update|UpdateRange)\(", File.ReadAllText(file));
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
