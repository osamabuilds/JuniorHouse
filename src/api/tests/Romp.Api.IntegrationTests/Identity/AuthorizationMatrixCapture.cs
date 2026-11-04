using Microsoft.AspNetCore.Hosting;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// Roles R4: writes the first proposal of the authorization matrix from the host's routes and the policies they carry today. It does
/// nothing unless <c>ROMP_MATRIX_OUT</c> names a file, so it is a tool kept beside the tests, not a check; the check is
/// <see cref="AuthorizationMatrixTests"/>. The proposal is mechanical and is reviewed by hand (R5) before any route is migrated.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class AuthorizationMatrixCapture(ProductionApiFixture api)
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public void ProposesAMatrix_WhenAskedTo()
    {
        var path = Environment.GetEnvironmentVariable("ROMP_MATRIX_OUT");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var current = AuthorizationMatrix.Capture(api.Factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).Services);
        var proposed = current.Select(row => row with { Permission = Propose(row) });
        File.WriteAllText(path, AuthorizationMatrix.ToCsv(proposed));
        File.WriteAllText(path + ".policies.txt", string.Join('\n', current.Select(r => $"{r.Method} {r.Route} :: {r.Permission} :: {r.Note}")));
    }

    private static string Propose(MatrixRow row)
    {
        if (row.Permission is MatrixRow.Anonymous or MatrixRow.Session || !row.Permission.StartsWith("UNMAPPED", StringComparison.Ordinal))
        {
            return row.Permission;
        }

        var route = row.Route;
        var read = row.Method == "GET";
        var policy = row.Note;

        string Area(string prefix, string area) => route.StartsWith(prefix, StringComparison.Ordinal) ? $"{area}.{(read ? "read" : "write")}" : string.Empty;

        if (policy.Contains("Admin", StringComparison.Ordinal))
        {
            return "ops.admin";
        }

        if (policy.Contains("Inventory", StringComparison.Ordinal))
        {
            return "inventory.write";
        }

        if (route.StartsWith("/api/warehouse/stock", StringComparison.Ordinal) || route.StartsWith("/api/warehouse/adjustments", StringComparison.Ordinal)
            || route.StartsWith("/api/warehouse/cycle-counts", StringComparison.Ordinal) || route.StartsWith("/api/warehouse/alerts", StringComparison.Ordinal))
        {
            return read ? "inventory.read" : "warehouse.write";
        }

        foreach (var (prefix, area) in new[]
                 {
                     ("/api/ref", "ref"), ("/api/catalog", "catalog"), ("/api/vendors", "procurement"), ("/api/purchase-orders", "procurement"),
                     ("/api/production", "production"), ("/api/quality", "qc"), ("/api/labelling", "labelling"), ("/api/warehouse", "warehouse"),
                     ("/api/admin/storefront", "catalog"), ("/api/storefront", "catalog"),
                 })
        {
            var proposal = Area(prefix, area);
            if (proposal.Length > 0)
            {
                return proposal;
            }
        }

        return row.Permission;
    }
}
