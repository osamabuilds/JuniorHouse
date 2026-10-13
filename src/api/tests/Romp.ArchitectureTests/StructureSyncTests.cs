using System.Text.RegularExpressions;

namespace Romp.ArchitectureTests;

/// <summary>
/// docs/CONSTITUTION.md section 5: the same module/feature names must identify a piece of
/// functionality in the specs, the API, the API tests, the web app and the e2e tests. These tests
/// read directories only (no database, no code).
/// </summary>
public sealed class StructureSyncTests
{
    private static readonly string[] NonFeatureFolders =
        ["Abstractions", "Migrations", "Persistence", "Support", "Properties", "bin", "obj"];

    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure"];

    [Fact]
    public void Constitution_Exists()
    {
        Assert.True(File.Exists(Path.Combine(RepoRoot, "docs", "CONSTITUTION.md")));
    }

    [Fact]
    public void EveryApiFeature_HasASpecFolderWithACard()
    {
        var missing = new List<string>();
        foreach (var (module, features) in ApiFeaturesByModule())
        {
            foreach (var feature in features)
            {
                var card = Path.Combine(SpecsRoot, Kebab(module), Kebab(feature), "README.md");
                if (!File.Exists(card))
                {
                    missing.Add($"{module}/{feature} -> {Relative(card)}");
                }
            }
        }

        Assert.True(missing.Count == 0, "API features without a spec card:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EverySpecFeature_HasAnApiFeature_OrIsMarkedPlanned()
    {
        var api = ApiFeaturesByModule().ToDictionary(p => Kebab(p.Key), p => p.Value.Select(Kebab).ToHashSet());
        var orphans = new List<string>();
        foreach (var moduleDir in Directory.GetDirectories(SpecsRoot).Where(d => !IsUnderscored(d)))
        {
            var module = Path.GetFileName(moduleDir);
            foreach (var featureDir in Directory.GetDirectories(moduleDir).Where(d => !IsUnderscored(d)))
            {
                var feature = Path.GetFileName(featureDir);
                var hasApi = api.TryGetValue(module, out var set) && set.Contains(feature);
                if (!hasApi && !IsPlanned(featureDir))
                {
                    orphans.Add($"{module}/{feature}");
                }
            }
        }

        Assert.True(orphans.Count == 0, "Spec features with no API feature (mark the card 'Planned' if intended):" + Environment.NewLine + string.Join(Environment.NewLine, orphans));
    }

    [Fact]
    public void EveryWebFeature_MatchesAnApiFeatureOfTheSameModule()
    {
        var api = ApiFeaturesByModule().ToDictionary(p => Kebab(p.Key), p => p.Value.Select(Kebab).ToHashSet());
        var bad = new List<string>();
        foreach (var moduleDir in Directory.GetDirectories(WebFeaturesRoot))
        {
            var module = Path.GetFileName(moduleDir);
            foreach (var featureDir in Directory.GetDirectories(moduleDir))
            {
                var feature = Path.GetFileName(featureDir);
                if (!(api.TryGetValue(module, out var set) && set.Contains(feature)))
                {
                    bad.Add($"{module}/{feature}");
                }
            }
        }

        Assert.True(bad.Count == 0, "Web features with no API feature of the same module:" + Environment.NewLine + string.Join(Environment.NewLine, bad));
    }

    [Fact]
    public void EveryApiTestFolder_MatchesAnApiFeature()
    {
        var bad = new List<string>();
        var testsRoot = Path.Combine(RepoRoot, "src", "api", "tests");
        foreach (var project in Directory.GetDirectories(testsRoot, "Romp.Modules.*.Tests"))
        {
            var module = Path.GetFileName(project).Split('.')[2];
            var features = ApiFeaturesByModule().First(p => p.Key == module).Value;
            foreach (var dir in Directory.GetDirectories(project).Select(Path.GetFileName).OfType<string>().Where(n => !NonFeatureFolders.Contains(n)))
            {
                if (!features.Contains(dir))
                {
                    bad.Add($"{module}/{dir}");
                }
            }
        }

        Assert.True(bad.Count == 0, "API test folders with no API feature:" + Environment.NewLine + string.Join(Environment.NewLine, bad));
    }

    [Fact]
    public void E2eSpecs_LiveInAModuleFolderOrJourneys_AndNameAFeature()
    {
        var api = ApiFeaturesByModule().ToDictionary(p => Kebab(p.Key), p => p.Value.Select(Kebab).ToHashSet());
        var bad = new List<string>();
        var e2e = Path.Combine(RepoRoot, "src", "web", "e2e");
        foreach (var file in Directory.GetFiles(e2e, "*.e2e.spec.ts", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(e2e, file).Replace('\\', '/');
            var parts = rel.Split('/');
            if (parts.Length != 2)
            {
                bad.Add(rel + " (must be e2e/<module>/<feature>.e2e.spec.ts or e2e/journeys/<journey>.e2e.spec.ts)");
                continue;
            }

            if (parts[0] == "journeys")
            {
                continue;
            }

            var feature = parts[1].Split('.')[0];
            if (!(api.TryGetValue(parts[0], out var set) && set.Contains(feature)))
            {
                bad.Add(rel + " (no API feature " + parts[0] + "/" + feature + ")");
            }
        }

        Assert.True(bad.Count == 0, "e2e files out of place:" + Environment.NewLine + string.Join(Environment.NewLine, bad));
    }

    [Fact]
    public void NoFolderOrFileName_CarriesATicketKeyOrSprint_OutsideLegacySprintSpecs()
    {
        var roots = new[]
        {
            SpecsRoot,
            WebFeaturesRoot,
            Path.Combine(RepoRoot, "src", "web", "e2e"),
        };
        var pattern = new Regex(@"(scrum-\d+|sprint)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var bad = new List<string>();
        foreach (var root in roots)
        {
            foreach (var path in Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (rel.Split('/').Any(IsLegacyContainer))
                {
                    continue;
                }

                if (pattern.IsMatch(Path.GetFileName(path)))
                {
                    bad.Add(Relative(path));
                }
            }
        }

        Assert.True(bad.Count == 0, "Names with a ticket key or 'sprint':" + Environment.NewLine + string.Join(Environment.NewLine, bad));
    }

    private static string SpecsRoot => Path.Combine(RepoRoot, "docs", "specs");

    private static string WebFeaturesRoot => Path.Combine(RepoRoot, "src", "web", "projects", "admin", "src", "app", "features");

    private static bool IsUnderscored(string dir) => Path.GetFileName(dir).StartsWith('_');

    private static bool IsLegacyContainer(string name) => name == "_sprint-specs" || name == "_template";

    private static bool IsPlanned(string featureDir)
    {
        var card = Path.Combine(featureDir, "README.md");
        return File.Exists(card) && File.ReadAllText(card).Contains("**Status:** Planned", StringComparison.Ordinal);
    }

    /// <summary>Module name (PascalCase) -> top-level feature folders found in its Domain/Application/Infrastructure projects.</summary>
    private static List<KeyValuePair<string, HashSet<string>>> ApiFeaturesByModule()
    {
        var result = new List<KeyValuePair<string, HashSet<string>>>();
        var modulesRoot = Path.Combine(RepoRoot, "src", "api", "src", "Modules");
        foreach (var moduleDir in Directory.GetDirectories(modulesRoot))
        {
            var module = Path.GetFileName(moduleDir);
            var features = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in Layers)
            {
                var project = Path.Combine(moduleDir, $"Romp.Modules.{module}.{layer}");
                if (!Directory.Exists(project))
                {
                    continue;
                }

                foreach (var dir in Directory.GetDirectories(project).Select(Path.GetFileName).OfType<string>())
                {
                    if (!NonFeatureFolders.Contains(dir))
                    {
                        features.Add(dir);
                    }
                }
            }

            result.Add(new KeyValuePair<string, HashSet<string>>(module, features));
        }

        return result;
    }

    private static string Kebab(string pascal) =>
        Regex.Replace(pascal, "(?<!^)([A-Z])", "-$1", RegexOptions.CultureInvariant).ToLowerInvariant();

    private static string Relative(string path) => Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (CLAUDE.md) not found above " + AppContext.BaseDirectory);
    }
}
