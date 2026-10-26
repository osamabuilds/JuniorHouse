using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Roles R8 (AC-9, AC-10): the checked-in authorization matrix and the routes of the host must say the same thing. A new route that is
/// not in the matrix, a route whose permission changed, a matrix line for a route that no longer exists, and an open route that is not
/// on the explicit anonymous list all fail the build.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class AuthorizationMatrixTests(ProductionApiFixture api)
{
    /// <summary>The only routes that may be called without signing in (AC-10): probes, sign-in, refresh, sign-out, the public storefront.</summary>
    private static readonly string[] AnonymousPrefixes =
    [
        "/health/",
        "/api/storefront/",
        "/media/",
    ];

    private static readonly string[] AnonymousRoutes =
    [
        "POST /api/auth/sign-in",
        "POST /api/auth/mfa/verify",
        "POST /api/auth/refresh",
        "POST /api/auth/sign-out",
    ];

    private static string MatrixPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root (CLAUDE.md) not found."),
            "docs", "specs", "identity", "roles", "authorization-matrix.csv");
    }

    private static IReadOnlyList<MatrixRow> Checked() => AuthorizationMatrix.ParseCsv(File.ReadAllText(MatrixPath()));

    private IReadOnlyList<MatrixRow> Live() => AuthorizationMatrix.Capture(api.Services);

    [Fact]
    [Trait("Spec", "AC-9")]
    public void EveryEndpointIsInTheMatrix()
    {
        var matrix = Checked().Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var live = Live().Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        var missing = live.Except(matrix).Order(StringComparer.Ordinal).ToList();
        var stale = matrix.Except(live).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, "Routes with no line in authorization-matrix.csv:\n" + string.Join('\n', missing));
        Assert.True(stale.Count == 0, "Matrix lines for routes that no longer exist:\n" + string.Join('\n', stale));
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void PermissionsMatch()
    {
        var matrix = Checked().ToDictionary(r => r.Key, r => r.Permission, StringComparer.Ordinal);

        var differing = Live()
            .Where(r => matrix.TryGetValue(r.Key, out var expected) && expected != r.Permission)
            .Select(r => $"{r.Key}: matrix says {matrix[r.Key]}, the route says {r.Permission}")
            .ToList();

        Assert.True(differing.Count == 0, "Routes whose permission differs from the matrix:\n" + string.Join('\n', differing));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void OnlyTheAnonymousListIsAnonymous()
    {
        bool Allowed(MatrixRow row) =>
            AnonymousRoutes.Contains(row.Key, StringComparer.Ordinal) || AnonymousPrefixes.Any(p => row.Route.StartsWith(p, StringComparison.Ordinal));

        var openInMatrix = Checked().Where(r => r.Permission == MatrixRow.Anonymous && !Allowed(r)).Select(r => r.Key).ToList();
        var openLive = Live().Where(r => r.Permission == MatrixRow.Anonymous && !Allowed(r)).Select(r => r.Key).ToList();

        Assert.True(openInMatrix.Count == 0, "The matrix opens routes that are not on the anonymous list:\n" + string.Join('\n', openInMatrix));
        Assert.True(openLive.Count == 0, "Open routes that are not on the anonymous list:\n" + string.Join('\n', openLive));
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void TheMatrix_IsWellFormed_AndNamesOnlyKnownPermissions()
    {
        var known = Romp.BuildingBlocks.Web.Permissions.All.Select(p => p.Value).Append(MatrixRow.Anonymous).Append(MatrixRow.Session).ToHashSet(StringComparer.Ordinal);
        var rows = Checked();

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count()); // one line per route
        Assert.DoesNotContain(rows, r => !known.Contains(r.Permission));
        Assert.All(rows, r => Assert.StartsWith("/", r.Route, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void NoMutatingRoute_NeedsOnlyAReadPermission()
    {
        var offenders = Checked()
            .Where(r => r.Method is "POST" or "PUT" or "PATCH" or "DELETE")
            .Where(r => r.Permission.EndsWith(".read", StringComparison.Ordinal))
            .Where(r => r.Key != "POST /api/catalog/slugs/suggest") // a query: it only proposes a slug (reviewed, authorization-matrix.md)
            .Select(r => r.Key)
            .ToList();

        Assert.True(offenders.Count == 0, "Mutating routes that need only a read permission:\n" + string.Join('\n', offenders));
    }
}
