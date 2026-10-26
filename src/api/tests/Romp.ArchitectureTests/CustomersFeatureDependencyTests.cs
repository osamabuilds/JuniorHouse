using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Romp.ArchitectureTests;

/// <summary>
/// docs/specs/customers/plan.md and the constitution (section 6): the Customers features depend on each other one way only. Only
/// wishlist may use accounts (its sign-in hook); every other feature knows none of the others. Shared code lives in Abstractions and
/// Emails, which are not features. Domain stays pure, and Customers reads other modules only through their <c>*.Contracts</c>.
/// </summary>
public sealed class CustomersFeatureDependencyTests
{
    private const string App = "Romp.Modules.Customers.Application";
    private const string Dom = "Romp.Modules.Customers.Domain";
    private const string Infra = "Romp.Modules.Customers.Infrastructure";

    private static readonly System.Reflection.Assembly Application = typeof(Romp.Modules.Customers.Application.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Domain = typeof(Romp.Modules.Customers.Domain.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(Romp.Modules.Customers.Infrastructure.CustomersModule).Assembly;

    // child-profiles is parked by the owner (ADR 0021); add "ChildProfiles" here when it is unparked.
    private static readonly string[] Features = ["Accounts", "Addresses", "Wishlist", "Newsletter", "NotifyMe"];
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure"];
    private static readonly string[] OtherModules = ["Catalog", "Warehouse", "Reference", "Vendor", "Production", "Quality", "Labelling", "Storefront", "Identity"];
    private static readonly System.Reflection.Assembly[] CustomersAssemblies = [Domain, Application, Infrastructure];

    /// <summary>For each feature: the features it may use. Everything else is forbidden.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["Accounts"] = [],
        ["Addresses"] = [],
        ["Wishlist"] = ["Accounts"],
        ["Newsletter"] = [],
        ["NotifyMe"] = [],
    };

    public static TheoryData<string> AllFeatures => new(Features);

    [Theory]
    [MemberData(nameof(AllFeatures))]
    public void ApplicationFeatures_DependOnlyOnTheirAllowedFeatures(string feature)
    {
        var forbidden = Forbidden(feature, App);

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
    public void InfrastructureFeatures_DependOnlyOnTheirAllowedFeatures(string feature)
    {
        var forbidden = Forbidden(feature, Infra);

        var result = Types.InAssembly(Infrastructure)
            .That().ResideInNamespace($"{Infra}.{feature}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} (Infrastructure) must not depend on {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CustomersDomain_DependsOnNoFrameworkOrOtherModule()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny(
                ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR", "FluentValidation", "Npgsql", .. OtherModules.Select(m => $"Romp.Modules.{m}")])
            .GetResult();

        Assert.True(result.IsSuccessful, "The Domain must stay pure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void CustomersApplication_NeverReferencesInfrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn(Infra)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application depends on its own ports, never on Infrastructure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void CustomersLayers_ReadOtherModules_OnlyThroughTheirContracts()
    {
        var internals = Layers
            .SelectMany(layer => OtherModules.Select(module => $"Romp.Modules.{module}.{layer}"))
            .ToArray();

        foreach (var assembly in CustomersAssemblies)
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(internals).GetResult();

            Assert.True(result.IsSuccessful, $"{assembly.GetName().Name} must use other modules' *.Contracts only: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void NoOtherModule_ReadsTheCustomersSchema()
    {
        var modulesRoot = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modulesRoot, file).Replace('\\', '/');
            if (relative.StartsWith("Customers/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), "\"CUST\"|Romp\\.Modules\\.Customers\\.(Domain|Application|Infrastructure)", RegexOptions.None, TimeSpan.FromSeconds(5)))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, "Only Customers may touch CUST or its internals; other modules use events or Customers.Contracts: " + string.Join(", ", offenders));
    }

    [Fact]
    public void CustomersSources_NeverQueryAnotherModulesSchema()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Customers");
        var otherSchemas = new Regex("\"(CTLG|WHSE|VNDR|PROD|QLTY|LBLG|REF|STFR|IDNT)\"\\.|FROM\\s+\"(CTLG|WHSE|VNDR|PROD|QLTY|LBLG|REF|STFR|IDNT)\"", RegexOptions.None, TimeSpan.FromSeconds(5));
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

        Assert.True(offenders.Count == 0, "Customers reads other modules through their Contracts, never their tables: " + string.Join(", ", offenders));
    }

    private static string[] Forbidden(string feature, string layerNamespace) =>
        Features.Where(f => f != feature && !Allowed[feature].Contains(f)).Select(f => $"{layerNamespace}.{f}").ToArray();

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
