using System.Text.RegularExpressions;

namespace Romp.ArchitectureTests;

/// <summary>
/// Roles R7 (AC-4, AC-12): access is decided by permissions, from the database, in one place. No role names in code, none of the old
/// role-shaped policies, and every mutating route of a module states the permission that lets it write. These tests read source files,
/// so they run in milliseconds; the live routes are checked against the authorization matrix by <c>AuthorizationMatrixTests</c>.
/// </summary>
public sealed class PermissionArchitectureTests
{
    private static readonly Regex RoleCheck = new(
        @"\.RequireRole\(|\.IsInRole\(|ClaimTypes\.Role\b|\bRoles\s*=\s*""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OldPolicy = new(
        @"Policies\.(Staff|Admin|Inventory|Catalogue|StorefrontSettings|StorefrontSearch)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MutatingRoute = new(@"\.Map(Post|Put|Patch|Delete)\(", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Routes that change nothing although they are POSTs (reviewed in <c>authorization-matrix.md</c>).</summary>
    private static readonly string[] QueryLikePosts = ["\"/slugs/suggest\""];

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (CLAUDE.md) not found.");
    }

    private static IEnumerable<string> SourceFiles(params string[] roots) =>
        roots.SelectMany(r => Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "api", r), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Fact]
    [Trait("Spec", "AC-4")]
    public void NoRoleStrings()
    {
        var offenders = SourceFiles("src", "tests")
            .Where(f => !f.EndsWith("PermissionArchitectureTests.cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
            .Where(t => RoleCheck.IsMatch(t.line))
            .Select(t => $"{Path.GetRelativePath(RepositoryRoot(), t.f)}:{t.i + 1}: {t.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "Role names or role checks (use a permission instead):" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void OldPoliciesAreGone()
    {
        var offenders = SourceFiles("src", "tests")
            .Where(f => !f.EndsWith("PermissionArchitectureTests.cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
            .Where(t => OldPolicy.IsMatch(t.line))
            .Select(t => $"{Path.GetRelativePath(RepositoryRoot(), t.f)}:{t.i + 1}: {t.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "Old role-shaped policy names:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));

        // The policy class itself names nothing but the explicit "open to everyone".
        var policies = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "api", "src", "BuildingBlocks", "Romp.BuildingBlocks.Web", "Policies.cs"));
        Assert.Single(Regex.Matches(policies, @"public const string \w+ = ""[^""]+"";"), m => !m.Value.Contains("strict", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void WritesNeedWritePermissions()
    {
        // Every POST, PUT, PATCH and DELETE route of a module (except the reviewed query-like ones) names its permission on the route itself.
        var offenders = new List<string>();
        foreach (var file in SourceFiles(Path.Combine("src", "Modules")).Where(f => f.Contains($"{Path.DirectorySeparatorChar}Infrastructure", StringComparison.Ordinal)))
        {
            if (file.Contains($"Identity{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue; // Identity's routes mix session-only and permission routes; the authorization matrix test covers them one by one
            }

            var text = File.ReadAllText(file);
            foreach (Match route in MutatingRoute.Matches(text))
            {
                var end = text.IndexOf(';', route.Index);
                var statement = text[route.Index..end];
                var firstArgument = statement[(statement.IndexOf('(', StringComparison.Ordinal) + 1)..];
                if (QueryLikePosts.Any(q => firstArgument.StartsWith(q, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!statement.Contains("RequirePermission(", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryRoot(), file)}: {statement[..Math.Min(statement.Length, 80)].Replace('\n', ' ').Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Mutating routes without RequirePermission:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void PermissionsAreDecidedInOnePlace()
    {
        // The only authorization handlers of the host: the Identity module's (database, per request) and the development stand-in.
        var handlers = SourceFiles("src")
            .Where(f => File.ReadAllText(f).Contains(": AuthorizationHandler<PermissionRequirement>", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["DevelopmentPermissionHandler.cs", "IdentityPermissionHandler.cs"], handlers);
    }
}
