using System.Reflection;

namespace Romp.Modules.Identity.Domain.StaffAccounts;

/// <summary>
/// The common-password list compiled into the assembly (<c>common-passwords.txt</c>): the entries of SecLists
/// <c>Pwdb_top-100000</c> (MIT licence) that are at least 12 characters long, lower-cased. Shorter ones are
/// already refused by the length rule.
/// </summary>
public sealed class EmbeddedCommonPasswords : ICommonPasswords
{
    private const string ResourceSuffix = "common-passwords.txt";

    private static readonly Lazy<HashSet<string>> Entries = new(Load);

    public bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return Entries.Value.Contains(password);
    }

    private static HashSet<string> Load()
    {
        var assembly = typeof(EmbeddedCommonPasswords).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var entry = line.Trim();
            if (entry.Length > 0)
            {
                set.Add(entry);
            }
        }

        return set;
    }
}
