using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Web;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>One line of the authorization matrix: a route of the host and what protects it.</summary>
internal sealed record MatrixRow(string Method, string Route, string Permission, string Note)
{
    public const string Anonymous = "anonymous";

    /// <summary>Any signed-in caller of any scope may call it (sign-out, the caller's own profile, own password, MFA enrolment).</summary>
    public const string Session = "session";

    public string Key => $"{Method} {Route}";
}

/// <summary>
/// Reads the host's endpoints (roles R4/R8, AC-9): method, route pattern and the permission each route needs, taken from the
/// <see cref="PermissionMetadata"/> it declares. A route with no permission is either on the explicit anonymous list or "session"
/// (authenticated only); anything else is a gap the matrix test reports.
/// </summary>
internal static class AuthorizationMatrix
{
    public static IReadOnlyList<MatrixRow> Capture(IServiceProvider services)
    {
        var rows = new List<MatrixRow>();
        foreach (var endpoint in services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"];
            var permission = endpoint.Metadata.GetOrderedMetadata<PermissionMetadata>() is { Count: > 0 } declared ? declared[^1].Code.Value : null;
            var authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var policies = string.Join('+', authorize.Select(a => a.Policy).Where(p => !string.IsNullOrEmpty(p)).Distinct());

            // A route that asks for a signed-in caller without naming a policy (the default policy) is "session" even inside a group
            // that is public; a route with no authorization at all is open and must be on the explicit anonymous list.
            var effective = permission
                ?? (authorize.Any(a => string.IsNullOrEmpty(a.Policy)) ? MatrixRow.Session
                    : anonymous || policies.Contains(Policies.Public, StringComparison.Ordinal) || authorize.Count == 0 ? MatrixRow.Anonymous
                    : $"UNMAPPED({policies})");
            var note = policies.Length > 0 ? $"policy {policies}" : string.Empty;

            foreach (var method in methods.Where(m => m != "HEAD" && m != "OPTIONS"))
            {
                rows.Add(new MatrixRow(method, endpoint.RoutePattern.RawText ?? string.Empty, effective, note));
            }
        }

        return [.. rows.OrderBy(r => r.Route, StringComparer.Ordinal).ThenBy(r => r.Method, StringComparer.Ordinal)];
    }

    public static string ToCsv(IEnumerable<MatrixRow> rows)
    {
        var csv = new StringBuilder("method,route,permission\n");
        foreach (var row in rows)
        {
            csv.Append(row.Method).Append(',').Append(row.Route).Append(',').Append(row.Permission).Append('\n');
        }

        return csv.ToString();
    }

    public static IReadOnlyList<MatrixRow> ParseCsv(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1)
            .Select(line => line.Split(','))
            .Select(parts => new MatrixRow(parts[0], parts[1], parts[2], string.Empty))
            .ToList();
}
