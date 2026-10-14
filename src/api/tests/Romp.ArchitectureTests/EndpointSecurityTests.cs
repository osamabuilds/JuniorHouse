using System.Text.RegularExpressions;

namespace Romp.ArchitectureTests;

/// <summary>
/// ADR 0011: every endpoint is mapped through <c>MapSecuredGroup</c>, so it carries an authorization policy. These tests read
/// source files, so they run in milliseconds and cannot be skipped by forgetting to register an endpoint in a test host.
/// </summary>
public sealed class EndpointSecurityTests
{
    private static readonly Regex BareMapping = new(
        @"\bendpoints\.Map(Group|Get|Post|Put|Patch|Delete)\(", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void ModuleEndpoints_AreMappedThroughMapSecuredGroup()
    {
        var modulesRoot = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (BareMapping.IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetRelativePath(modulesRoot, file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Use MapSecuredGroup (ADR 0011):" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Host_MapsNoAnonymousApiEndpoints_ExceptHealth()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "api", "src", "Romp.Api", "Program.cs"));
        var bare = Regex.Matches(program, @"\bapp\.Map(Get|Post|Put|Patch|Delete|Group)\(\s*""(/api[^""]*)""");
        Assert.True(bare.Count == 0, "Program.cs maps an /api endpoint without a policy: " + string.Join(", ", bare.Select(m => m.Groups[2].Value)));
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
