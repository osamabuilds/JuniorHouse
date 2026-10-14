using NetArchTest.Rules;
using Romp.Modules.Labelling.Application;

namespace Romp.ArchitectureTests;

/// <summary>
/// Docs/specs/labelling/plan.md and the constitution (section 6): the four Labelling features depend on each other in ONE
/// direction only, through each callee's public interface:
/// <c>label-work → {print-jobs, variants, tags}</c>, <c>print-jobs → {variants, tags}</c>, <c>tags → variants</c>, and
/// <c>variants</c> knows none of them. A cycle or a back-reference fails the build.
/// </summary>
public sealed class LabellingFeatureDependencyTests
{
    private const string App = "Romp.Modules.Labelling.Application";
    private const string Dom = "Romp.Modules.Labelling.Domain";

    private static readonly System.Reflection.Assembly Application = typeof(AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Domain = typeof(Romp.Modules.Labelling.Domain.GtinStatus).Assembly;

    public static TheoryData<string, string[]> Forbidden => new()
    {
        // feature (Application layer)                 must not use
        { $"{App}.Variants", [$"{App}.LabelWork", $"{App}.PrintJobs", $"{App}.Tags"] },
        { $"{App}.Tags", [$"{App}.LabelWork", $"{App}.PrintJobs"] },
        { $"{App}.PrintJobs", [$"{App}.LabelWork"] },
    };

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void ApplicationFeatures_DependOnlyDownwards(string feature, string[] forbidden)
    {
        var result = Types.InAssembly(Application)
            .That().ResideInNamespace(feature)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} must not depend on {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [InlineData($"{Dom}.Variants", $"{Dom}.LabelWork;{Dom}.PrintJobs;{Dom}.Tags")]
    [InlineData($"{Dom}.Tags", $"{Dom}.LabelWork;{Dom}.PrintJobs")]
    [InlineData($"{Dom}.PrintJobs", $"{Dom}.LabelWork;{Dom}.Tags")]
    [InlineData($"{Dom}.LabelWork", $"{Dom}.Variants;{Dom}.PrintJobs;{Dom}.Tags")]
    public void DomainFeatures_DependOnlyDownwards(string feature, string forbiddenList)
    {
        var forbidden = forbiddenList.Split(';');
        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace(feature)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, $"{feature} must not depend on {forbiddenList}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    [Trait("Spec", "TAG-9")]
    public void TagBuilding_HasNoDependencyOnPrintersFilesOrTheNetwork()
    {
        var result = Types.InAssembly(Application)
            .That().ResideInNamespace($"{App}.Tags")
            .ShouldNot().HaveDependencyOnAny(
                "System.Net", "System.IO", "Microsoft.EntityFrameworkCore", "Romp.BuildingBlocks.Storage", $"{App}.PrintJobs")
            .GetResult();

        Assert.True(result.IsSuccessful, "Tag building must stay pure and testable in isolation: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void LabellingDomain_DependsOnNoFrameworkOrOtherModule()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR", "FluentValidation", "Npgsql",
                "Romp.Modules.Vendor", "Romp.Modules.Catalog", "Romp.Modules.Reference", "Romp.Modules.Quality", "Romp.Modules.Production")
            .GetResult();

        Assert.True(result.IsSuccessful, "The Domain must stay pure: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
