namespace Romp.ArchitectureTests;

/// <summary>
/// identity/audit-trail AC-2, AC-3: every module saves through the audit collector, so a new module cannot be forgotten. Reads source
/// files, so it runs in milliseconds; the behaviour itself is proved against PostgreSQL by the collector and emission tests.
/// </summary>
public sealed class AuditArchitectureTests
{
    private static string ModulesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root (CLAUDE.md) not found."), "src", "api", "src", "Modules");
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    [Trait("Spec", "AC-3")]
    public void EveryModule_RegistersTheAuditCollector_NextToTheOutbox()
    {
        var missing = new List<string>();
        foreach (var file in Directory.GetFiles(ModulesRoot(), "*Module.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("OutboxSaveChangesInterceptor", StringComparison.Ordinal)
                && !text.Contains("AuditTrailSaveChangesInterceptor", StringComparison.Ordinal))
            {
                missing.Add(Path.GetFileName(file));
            }
        }

        Assert.True(missing.Count == 0, "Modules that write an outbox but do not register AuditTrailSaveChangesInterceptor: " + string.Join(", ", missing));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void NothingInTheCode_UpdatesOrDeletesAuditRows()
    {
        var offenders = Directory.GetFiles(ModulesRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("AuditEntries.Remove", StringComparison.Ordinal)
                       || text.Contains("AuditEntries.Update", StringComparison.Ordinal)
                       || text.Contains("UPDATE \"IDNT\".\"AUDT_", StringComparison.Ordinal)
                       || text.Contains("DELETE FROM \"IDNT\".\"AUDT_", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Code that changes the audit trail: " + string.Join(", ", offenders));
    }
}
