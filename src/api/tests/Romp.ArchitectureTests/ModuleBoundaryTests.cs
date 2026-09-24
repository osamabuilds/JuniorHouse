using System.Reflection;
using NetArchTest.Rules;

namespace Romp.ArchitectureTests;

/// <summary>
/// Enforces Clean Architecture layering inside each module (BRD §9.1). Add each new module's
/// assemblies to <see cref="Modules"/> as it is created.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] ModuleNames = ["Catalog"];

    public static TheoryData<string> Modules => new(ModuleNames);

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_DoesNotDependOnApplicationOrInfrastructure(string module)
    {
        var result = Types.InAssembly(LoadLayer(module, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Romp.Modules.{module}.Application",
                $"Romp.Modules.{module}.Infrastructure",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_DoesNotDependOnInfrastructure(string module)
    {
        var result = Types.InAssembly(LoadLayer(module, "Application"))
            .ShouldNot()
            .HaveDependencyOn($"Romp.Modules.{module}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_DoesNotDependOnOtherModules(string module)
    {
        var otherModules = ModuleNames
            .Where(other => other != module)
            .Select(other => $"Romp.Modules.{other}")
            .ToArray();

        if (otherModules.Length == 0)
        {
            return;
        }

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
        {
            var result = Types.InAssembly(LoadLayer(module, layer))
                .ShouldNot()
                .HaveDependencyOnAny(otherModules)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }
    }

    private static Assembly LoadLayer(string module, string layer) =>
        Assembly.Load($"Romp.Modules.{module}.{layer}");

    private static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
