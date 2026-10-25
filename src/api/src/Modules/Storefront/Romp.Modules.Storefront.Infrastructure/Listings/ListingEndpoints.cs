using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Storage;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.Listings;

public sealed class ListingSettings(IOptions<StorefrontOptions> options) : IListingSettings
{
    public int NewArrivalsDays => options.Value.NewArrivalsDays;
}

/// <summary>
/// The anonymous listing reads and the derivative files they point at. Every input is parsed against allow-lists; nothing the visitor typed
/// is echoed (LST-11). A scope that does not exist and a page after the last are real 404s (LST-14).
/// </summary>
public static class ListingEndpoints
{
    private static readonly HashSet<string> Scopes = ["audience", "category", "new-in", "sale", "collection"];

    public static void MapPublicListingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(StorefrontSettingsEndpoints.PublicPrefix + "/listings", Policies.Public).WithTags("Storefront");

        group.MapGet("", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var (scope, filter, problem) = Parse(http.Request.Query);
            if (problem is not null)
            {
                return problem;
            }

            var result = await sender.Send(new SearchListingQuery(scope!, filter!), cancellationToken);
            return Answer(http, result);
        });

        group.MapGet("/options", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var (scope, _, problem) = Parse(http.Request.Query);
            if (problem is not null)
            {
                return problem;
            }

            return Answer(http, await sender.Send(new GetListingOptionsQuery(scope!), cancellationToken));
        });

        group.MapGet("/empty", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var (scope, _, problem) = Parse(http.Request.Query);
            if (problem is not null)
            {
                return problem;
            }

            return Answer(http, await sender.Send(new GetEmptyStateQuery(scope!), cancellationToken));
        });

        // Derivative files by their generated, content-hashed name: the name never changes for the same bytes, so they cache for a year (PCT-7).
        endpoints.MapSecuredGroup("/media", Policies.Public).WithTags("Storefront").MapGet("/{name}", async (string name, IFileStorage storage, CancellationToken cancellationToken) =>
        {
            if (!DerivativeName.TryParse(name, out _, out _, out var format))
            {
                return Results.NotFound();
            }

            try
            {
                var bytes = await storage.RetrieveAsync(name, cancellationToken);
                return Results.File(bytes, format == DerivativeFormat.Webp ? "image/webp" : "image/jpeg", enableRangeProcessing: false,
                    entityTag: null, lastModified: null);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                return Results.NotFound();
            }
        });
    }

    private static IResult Answer<T>(HttpContext http, ListingResult<T> result)
        where T : class
    {
        if (result.Outcome != ListingOutcome.Found)
        {
            return Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
        }

        http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
        return Results.Ok(result.Value);
    }

    /// <summary>Reads the scope and filter from the query. The third item is the 400 to return, or null when the input is fine.</summary>
    private static (ListingScope? Scope, ListingFilter? Filter, IResult? Problem) Parse(IQueryCollection query)
    {
        string? One(string key) => query.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : null;

        var kind = One("scope");
        if (kind is null || !Scopes.Contains(kind))
        {
            return (null, null, BadRequest("scope", "The scope is audience, category, new-in, sale or collection."));
        }

        var gender = Segment(One("gender"));
        var category = Segment(One("category"));
        var collection = Segment(One("collection"));
        ListingScope? scope = kind switch
        {
            "audience" when gender is not null => ListingScope.Audience(gender),
            "category" when gender is not null && category is not null => ListingScope.InCategory(gender, category),
            "new-in" => ListingScope.NewIn(gender),
            "sale" => ListingScope.Sale(gender),
            "collection" when collection is not null => ListingScope.OfCollection(collection),
            _ => null,
        };

        if (scope is null)
        {
            return (null, null, BadRequest("scope", "The scope is missing a code."));
        }

        var (filter, errors) = ListingFilter.Parse(
            query["size"], query["colour"], One("age"), One("price"), One("sort"), One("page"), One("pageSize"));
        if (filter is null)
        {
            return (scope, null, Results.ValidationProblem(
                errors.GroupBy(e => e.Field).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray())));
        }

        return (scope, filter, null);
    }

    /// <summary>A code from the URL, or null when it is not a plausible one (it then simply cannot match a lookup row).</summary>
    private static string? Segment(string? value) =>
        value is { Length: > 0 and <= 40 } v && v.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-') ? v : null;

    private static IResult BadRequest(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
