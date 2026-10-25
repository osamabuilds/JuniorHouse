using System.Reflection;
using System.Text.RegularExpressions;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>
/// Sprint 8 task G4: the permission codes endpoints declare (roles spec, "Permission catalogue"). The codes become lookup rows in G5,
/// so they are stable, lower-case, <c>area.action</c> and short enough for the lookup <c>CODE</c> column (varchar(30)).
/// </summary>
public sealed partial class PermissionCodesTests
{
    [GeneratedRegex(@"^[a-z]+\.[a-z]+$")]
    private static partial Regex CodePattern();

    private static readonly string[] SpecCodes =
    [
        "ref.read", "ref.write", "catalog.read", "catalog.write", "procurement.read", "procurement.write", "production.read",
        "production.write", "qc.read", "qc.write", "labelling.read", "labelling.write", "warehouse.read", "warehouse.write",
        "inventory.read", "inventory.write", "ops.admin", "staff.manage", "audit.read",
    ];

    private static IEnumerable<string> Declared() =>
        typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PermissionCode))
            .Select(f => ((PermissionCode)f.GetValue(null)!).Value);

    [Fact]
    [Trait("Spec", "FR-60")]
    [Trait("Spec", "SEC-08")]
    public void Codes_AreExactlyTheRolesSpecCatalogue()
    {
        Assert.Equal(SpecCodes.Order(StringComparer.Ordinal), Declared().Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public void Codes_AreUniqueLowerCaseAreaDotAction_AndFitTheLookupColumn()
    {
        var codes = Declared().ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code =>
        {
            Assert.Matches(CodePattern(), code);
            Assert.True(code.Length <= 30, $"{code} is longer than the 30-character lookup CODE column");
        });
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public void All_ListsEveryDeclaredCode_AndNothingElse()
    {
        Assert.Equal(Declared().Order(StringComparer.Ordinal), Permissions.All.Select(p => p.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "FR-60")]
    public void EveryWritePermission_HasAMatchingReadPermission()
    {
        // A route that adds ".write" to a group declared with ".read" requires both, so a role can only use a write permission
        // together with its read permission (roles spec AC-8: least privilege adds permissions in pairs).
        var codes = Declared().ToHashSet(StringComparer.Ordinal);

        Assert.All(codes.Where(c => c.EndsWith(".write", StringComparison.Ordinal)), write =>
            Assert.Contains(write[..^".write".Length] + ".read", codes));
    }

    [Fact]
    public void ToString_IsTheCode_SoItNeverLeaksAsAStructName() =>
        Assert.Equal("catalog.write", Permissions.CatalogWrite.ToString());

    [Fact]
    public void ACodeCannotBeBuiltFromAnEmptyOrMalformedString()
    {
        Assert.Throws<ArgumentException>(() => new PermissionCode(""));
        Assert.Throws<ArgumentException>(() => new PermissionCode("Catalog.Write"));
        Assert.Throws<ArgumentException>(() => new PermissionCode("catalog"));
    }
}
