using System.Text.RegularExpressions;

namespace Romp.ArchitectureTests;

/// <summary>
/// Checks docs/db/naming.md itself: the abbreviation glossary is the only list of allowed words (section 1: "a word must never get a
/// second abbreviation"), so it must stay consistent, and every identifier a plan intends to create must be built from glossary
/// words before its migration is written. <see cref="NamingConventionTests"/> checks the shape of names in the EF models; this checks
/// the words.
/// </summary>
public sealed partial class GlossaryTests
{
    [GeneratedRegex("^[A-Z0-9]{2,4}$")]
    private static partial Regex AbbreviationPattern();

    /// <summary>The words Sprint 8 (Identity) adds, with the abbreviations the identity plan and the glossary must agree on.</summary>
    private static readonly (string Word, string Abbreviation)[] Sprint8Words =
    [
        ("absolute", "ABS"), ("access", "ACSS"), ("agent", "AGNT"), ("all", "ALL"), ("audit", "AUDT"), ("client", "CLNT"),
        ("correlation", "CORR"), ("display", "DISP"), ("encrypted", "ENC"), ("enrolled", "ENRL"),
        ("entity", "ENTY"), ("failed", "FAIL"), ("identity", "IDNT"), ("IP", "IP"), ("login", "LGIN"), ("multi-factor authentication", "MFA"),
        ("must", "MUST"), ("occurred", "OCCR"), ("operation", "OPRN"), ("password", "PWD"), ("permission", "PERM"),
        ("previous", "PREV"), ("recovery", "RCVR"), ("refresh", "RFSH"), ("revoked", "RVKD"), ("role", "ROLE"),
        ("secret", "SECR"), ("session", "SESS"), ("source", "SRC"), ("temporary", "TEMP"), ("token", "TOKN"), ("used", "USED"),
    ];

    /// <summary>
    /// Every table and column the identity plan (data model section) intends to create. Kept here as data so a word missing from the
    /// glossary fails before the migration exists; the migration tasks then add the real EF-model check through
    /// <see cref="NamingConventionTests"/>.
    /// </summary>
    private static readonly string[] PlannedIdentityIdentifiers =
    [
        "IDNT", "ROLE_LKP", "PERM_LKP", "ROLE_PERM_MAP", "USR_MAIN", "USR_PWD_HIST", "USR_ROLE_MAP", "USR_RCVR_CODE", "SESS_MAIN",
        "SESS_RVKD_RSN_LKP", "LGIN_PEND", "LGIN_ATMP", "LGIN_OTCM_LKP", "AUDT_ENTY", "AUDT_OPRN_LKP", "AUDT_CHNG", "OUTB_MSG", "INBX",
        "ALL_PERM_IND", "USR_NAME", "DISP_NAME", "PWD_HASH", "PWD_CHNG_DTE", "MUST_CHNG_PWD_IND", "TEMP_PWD_EXPY_DTE", "ACT_IND",
        "LAST_LGIN_DTE", "MFA_ENRL_DTE", "MFA_SECR_ENC", "MFA_KEY_ID", "MFA_LAST_STEP_NO", "CODE_HASH", "USED_DTE", "ACSS_TOKN_HASH",
        "ACSS_EXPY_DTE", "RFSH_TOKN_HASH", "PREV_RFSH_TOKN_HASH", "RFSH_EXPY_DTE", "ABS_EXPY_DTE", "RVKD_DTE", "RVKD_RSN_ID",
        "CLNT_IP_ADDR", "USR_AGNT", "TOKN_HASH", "EXPY_DTE", "FAIL_CNT", "ATMP_NAME", "OTCM_ID", "ATMP_DTE", "SRC_MSG_ID",
        "MODL_NAME", "ENTY_TYP_NAME", "ENTY_KEY", "OPRN_ID", "ACTR_NAME", "OCCR_DTE", "CORR_ID", "AUDT_ENTY_ID", "COLM_NAME",
    ];

    private static readonly Lazy<List<(string Word, string Abbreviation)>> Entries = new(ParseGlossary);

    [Fact]
    [Trait("Spec", "NFR-SC-05")]
    public void Glossary_HasNoWordWithTwoAbbreviations_AndNoAbbreviationForTwoWords()
    {
        var byAbbreviation = Entries.Value.GroupBy(e => e.Abbreviation).Where(g => g.Select(e => e.Word.ToLowerInvariant()).Distinct().Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(" / ", g.Select(e => e.Word))}").ToList();
        var byWord = Entries.Value.GroupBy(e => e.Word.ToLowerInvariant()).Where(g => g.Select(e => e.Abbreviation).Distinct().Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(" / ", g.Select(e => e.Abbreviation).Distinct())}").ToList();

        Assert.True(byAbbreviation.Count == 0, "Abbreviations used for two words:" + Environment.NewLine + string.Join(Environment.NewLine, byAbbreviation));
        Assert.True(byWord.Count == 0, "Words with two abbreviations:" + Environment.NewLine + string.Join(Environment.NewLine, byWord));
    }

    [Fact]
    [Trait("Spec", "NFR-SC-05")]
    public void Glossary_ContainsEverySprint8Word_WithTheAgreedAbbreviation()
    {
        var missing = Sprint8Words.Where(w => !Entries.Value.Any(e => e.Abbreviation == w.Abbreviation
            && e.Word.StartsWith(w.Word.Split(' ')[0], StringComparison.OrdinalIgnoreCase))).Select(w => $"{w.Word} = {w.Abbreviation}").ToList();

        Assert.True(missing.Count == 0, "Add these Sprint 8 words to docs/db/naming.md:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    [Trait("Spec", "NFR-SC-05")]
    public void PlannedIdentityIdentifiers_AreBuiltOnlyFromGlossaryAbbreviations()
    {
        var known = Entries.Value.Select(e => e.Abbreviation).ToHashSet();
        var unknown = PlannedIdentityIdentifiers
            .SelectMany(id => id.Split('_').Select(word => (Identifier: id, Word: word)))
            .Where(x => !known.Contains(x.Word))
            .Select(x => $"{x.Identifier}: {x.Word}")
            .ToList();

        Assert.True(unknown.Count == 0, "Words missing from the glossary:" + Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }

    [Fact]
    public void PlannedIdentityIdentifiers_FollowTheIdentifierPattern()
    {
        var bad = PlannedIdentityIdentifiers.Where(id => !Regex.IsMatch(id, "^[A-Z0-9]{2,4}(_[A-Z0-9]{2,4})*$") || id.Length > 63).ToList();

        Assert.True(bad.Count == 0, "Not valid identifiers: " + string.Join(", ", bad));
    }

    [Fact]
    [Trait("Spec", "NFR-SC-05")]
    public void SchemaTable_ListsStaffIdentityAndCustomersSeparately()
    {
        var lines = File.ReadAllLines(NamingFile());
        var schemas = lines.SkipWhile(l => !l.StartsWith("## 4.", StringComparison.Ordinal)).TakeWhile(l => !l.StartsWith("## 5.", StringComparison.Ordinal)).ToList();

        Assert.Contains(schemas, l => l.Contains("`IDNT`", StringComparison.Ordinal) && l.Contains("Identity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(schemas, l => l.Contains("`CUST`", StringComparison.Ordinal) && l.Contains("Customers", StringComparison.OrdinalIgnoreCase)
            && !l.Contains("IDNT", StringComparison.Ordinal));
    }

    private static List<(string Word, string Abbreviation)> ParseGlossary()
    {
        var lines = File.ReadAllLines(NamingFile()).SkipWhile(l => !l.StartsWith("## 5.", StringComparison.Ordinal));
        var entries = new List<(string Word, string Abbreviation)>();
        foreach (var line in lines.Where(l => l.StartsWith('|')))
        {
            var cells = line.Split('|').Skip(1).SkipLast(1).Select(c => c.Trim()).ToArray();
            foreach (var (wordIndex, abbreviationIndex) in new[] { (0, 1), (3, 4) })
            {
                if (cells.Length > abbreviationIndex && cells[wordIndex].Length > 0 && AbbreviationPattern().IsMatch(cells[abbreviationIndex]))
                {
                    entries.Add((cells[wordIndex], cells[abbreviationIndex]));
                }
            }
        }

        return entries;
    }

    private static string NamingFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "db", "naming.md")))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException("docs/db/naming.md not found above the test binaries") : Path.Combine(dir.FullName, "docs", "db", "naming.md");
    }
}
