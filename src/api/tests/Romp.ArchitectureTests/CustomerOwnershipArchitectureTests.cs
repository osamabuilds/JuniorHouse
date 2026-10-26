using System.Reflection;
using System.Text.RegularExpressions;
using MediatR;
using Romp.Modules.Customers.Application.Abstractions;

namespace Romp.ArchitectureTests;

/// <summary>
/// ADR 0021 §3, ACT-18, SEC-05: customer-owned data is scoped by construction. The customer comes from <see cref="ICurrentCustomer"/>
/// only: no request type, route or endpoint takes a customer id, and every query on a customer-owned set filters by it.
/// </summary>
public sealed class CustomerOwnershipArchitectureTests
{
    private static readonly System.Reflection.Assembly Application = typeof(Romp.Modules.Customers.Application.AssemblyReference).Assembly;
    private static readonly System.Reflection.Assembly Domain = typeof(Romp.Modules.Customers.Domain.AssemblyReference).Assembly;

    private static readonly string[] CustomerIdNames = ["CustomerId", "CustId", "OwnerId"];

    [Fact]
    [Trait("Spec", "ACT-18")]
    [Trait("Spec", "SEC-05")]
    public void NoRequest_TakesACustomerId()
    {
        var offenders = new List<string>();
        foreach (var type in Application.GetTypes().Where(t => typeof(IBaseRequest).IsAssignableFrom(t)))
        {
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)
                .Concat(type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.Name ?? string.Empty));
            if (members.Any(name => CustomerIdNames.Contains(name, StringComparer.OrdinalIgnoreCase)))
            {
                offenders.Add(type.FullName ?? type.Name);
            }
        }

        Assert.True(offenders.Count == 0, "The customer comes from ICurrentCustomer, never from the request: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "ACT-18")]
    [Trait("Spec", "SEC-05")]
    public void NoRoute_TakesACustomerId()
    {
        var offenders = new List<string>();
        var routeLiteral = new Regex("\"/[^\"]*\\{(customerId|custId|ownerId)[^}]*\\}[^\"]*\"", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
        foreach (var file in CustomersSources("Romp.Modules.Customers.Infrastructure"))
        {
            if (routeLiteral.IsMatch(File.ReadAllText(file)))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, "No route may carry a customer id: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "ACT-18")]
    public void EveryCustomerMeRoute_SitsInACustomerPolicyGroup()
    {
        var offenders = new List<string>();
        foreach (var file in CustomersSources("Romp.Modules.Customers.Infrastructure"))
        {
            foreach (var line in File.ReadAllLines(file).Where(l => l.Contains("\"/api/customers/me", StringComparison.Ordinal)))
            {
                if (!line.Contains("Policies.Customer", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {line.Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Every /api/customers/me group must use Policies.Customer: " + string.Join(" | ", offenders));
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void CustomerOwnedSets_AreOnlyQueriedWithTheCustomerFilter()
    {
        var ownedSets = OwnedSetNames();
        var offenders = new List<string>();
        foreach (var file in CustomersSources("Romp.Modules.Customers.Application"))
        {
            offenders.AddRange(OwnershipScan.FindUnfilteredFiles(Path.GetFileName(file), File.ReadAllText(file), ownedSets));
        }

        Assert.True(offenders.Count == 0, "Customer-owned sets must be filtered by the current customer in the same statement: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TheScanner_FlagsAnUnfilteredQuery_AndAcceptsAFilteredOne()
    {
        string[] sets = ["Addresses"];

        Assert.Empty(OwnershipScan.FindUnfilteredFiles("Ok.cs", "var id = current.Require(); await db.Addresses.Where(a => a.Id == x && a.CustomerId == id).ToListAsync();", sets));
        Assert.Single(OwnershipScan.FindUnfilteredFiles("Bad.cs", "await db.Addresses.Where(a => a.Id == x).ToListAsync();", sets));
        Assert.Empty(OwnershipScan.FindUnfilteredFiles("Using.cs", "using Romp.Modules.Customers.Domain.Addresses;\nnamespace Romp.Modules.Customers.Application.Addresses;", sets));
        Assert.Empty(OwnershipScan.FindUnfilteredFiles("None.cs", "await db.Customers.ToListAsync();", sets));
    }

    /// <summary>DbSet properties of the persistence port whose entity has a customer id (every customer-owned table).</summary>
    private static string[] OwnedSetNames()
    {
        var owned = Domain.GetTypes()
            .Where(t => t.GetProperty("CustomerId") is not null)
            .Select(t => t.Name)
            .ToHashSet();

        return typeof(ICustomerDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericArguments().Any(a => owned.Contains(a.Name)))
            .Select(p => p.Name)
            .ToArray();
    }

    private static IEnumerable<string> CustomersSources(string project)
    {
        var root = Path.Combine(FindRepoRoot(), "src", "api", "src", "Modules", "Customers", project);
        return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
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

/// <summary>Source scan: a file that touches a customer-owned DbSet must also filter by the customer id.</summary>
internal static class OwnershipScan
{
    public static IEnumerable<string> FindUnfilteredFiles(string fileName, string source, IEnumerable<string> ownedSets)
    {
        // Namespace and using lines name folders, not data access (Domain.Addresses is a namespace).
        var code = string.Join('\n', source.Split('\n').Where(l => !l.TrimStart().StartsWith("using ", StringComparison.Ordinal) && !l.TrimStart().StartsWith("namespace ", StringComparison.Ordinal)));
        var touchesOwnedSet = ownedSets.Any(set => Regex.IsMatch(code, $@"\.{Regex.Escape(set)}\b", RegexOptions.None, TimeSpan.FromSeconds(5)));
        if (touchesOwnedSet && !Regex.IsMatch(code, @"\.CustomerId\b", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            yield return fileName;
        }
    }
}
