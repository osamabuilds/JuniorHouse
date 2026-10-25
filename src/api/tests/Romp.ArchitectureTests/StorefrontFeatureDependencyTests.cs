using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Romp.ArchitectureTests;

/// <summary>
/// docs/specs/storefront/published-catalog/plan.md and the constitution (section 6): the Storefront's two features, the admin-managed
/// settings and the published-catalog read model, know nothing of each other. Neither module reads another module's tables; the read model
/// takes Catalog, Warehouse and Reference data only through their <c>*.Contracts</c> (ADR 0002, ADR 0018).
/// </summary>
public sealed class StorefrontFeatureDependencyTests
{
    private const string App = "Romp.Modules.Storefront.Application";
    private const string Dom = "Romp.Modules.Storefront.Domain";
    private const string Infra = "Romp.Modules.Storefront.Infrastructure";

    private static readonly System.Reflection.Assembly Application = typeof(Romp.Modules.Storefront.Application.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Domain = typeof(Romp.Modules.Storefront.Domain.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(Romp.Modules.Storefront.Infrastructure.StorefrontModule).Assembly;

    private static readonly string[] Features = ["Listings", "Navigation", "PublishedCatalog", "StorefrontSettings"];
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure"];
    private static readonly string[] OtherModules = ["Catalog", "Warehouse", "Reference", "Vendor", "Production", "Quality", "Labelling"];
    private static readonly System.Reflection.Assembly[] StorefrontAssemblies = [Domain, Application, Infrastructure];

    /// <summary>The one allowed Application dependency: the home page (Navigation) shows listing tiles. Listings never know Navigation.</summary>
    private static readonly Dictionary<string, string[]> AllowedApplication = new() { ["Navigation"] = ["Listings"] };

    public static TheoryData<string> AllFeatures => new(Features);

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void ApplicationFeatures_DependOnNoOtherFeature(string feature)
    {
        var allowed = AllowedApplication.GetValueOrDefault(feature, []);
        var forbidden = Features.Where(f => f != feature && !allowed.Contains(f)).Select(f => $"{App}.{f}").ToArray();

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

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void InfrastructureFeatures_DependOnNoOtherFeature(string feature)
    {
        var forbidden = Features.Where(f => f != feature).Select(f => $"{Infra}.{f}").ToArray();

        var result = Types.InAssembly(Infrastructure)
            .That().ResideInNamespace($"{Infra}.{feature}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} (Infrastructure) must not depend on another feature: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void StorefrontDomain_DependsOnNoFrameworkOrOtherModule()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR", "FluentValidation", "Npgsql",
                "Romp.Modules.Vendor", "Romp.Modules.Catalog", "Romp.Modules.Reference", "Romp.Modules.Quality", "Romp.Modules.Production",
                "Romp.Modules.Labelling", "Romp.Modules.Warehouse")
            .GetResult();

        Assert.True(result.IsSuccessful, "The Domain must stay pure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void StorefrontApplication_NeverReferencesInfrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn(Infra)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application depends on its own ports, never on Infrastructure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public void StorefrontApplicationAndDomain_ReadOtherModules_OnlyThroughTheirContracts()
    {
        var internals = Layers
            .SelectMany(layer => OtherModules.Select(module => $"Romp.Modules.{module}.{layer}"))
            .ToArray();

        foreach (var assembly in StorefrontAssemblies)
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(internals).GetResult();

            Assert.True(result.IsSuccessful, $"{assembly.GetName().Name} must use other modules' *.Contracts only: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public void NoOtherModule_ReadsTheStorefrontSchema()
    {
        var modulesRoot = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modulesRoot, file).Replace('\\', '/');
            if (relative.StartsWith("Storefront/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), "\"STFR\"|Romp\\.Modules\\.Storefront\\.(Domain|Application|Infrastructure)"))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, "Only Storefront may touch STFR or its internals; other modules use events or Storefront.Contracts: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public void StorefrontSources_NeverQueryAnotherModulesSchema()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Storefront");
        var otherSchemas = new Regex("\"(CTLG|WHSE|VNDR|PROD|QLTY|LBLG|REF)\"\\.|FROM\\s+\"(CTLG|WHSE|VNDR|PROD|QLTY|LBLG|REF)\"", RegexOptions.None, TimeSpan.FromSeconds(5));
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/Migrations/", StringComparison.Ordinal))
            {
                continue;
            }

            if (otherSchemas.IsMatch(File.ReadAllText(file)))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, "Storefront reads other modules through their Contracts, never their tables: " + string.Join(", ", offenders));
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
