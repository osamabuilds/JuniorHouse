namespace Romp.Modules.Catalog.Contracts;

/// <summary>What a public path means (URL-2, URL-9): the live page, an old address to redirect, a removed product, or nothing.</summary>
public enum PathOutcome
{
    /// <summary>The path is the product's current one: serve the page.</summary>
    Current = 1,

    /// <summary>An old path of a live product: answer 301 to <see cref="PathResolutionResult.CurrentPath"/>.</summary>
    Moved = 2,

    /// <summary>The product is archived for good: answer 410.</summary>
    Gone = 3,

    /// <summary>No such path: answer 404.</summary>
    NotFound = 4,
}

/// <summary>The answer to a path lookup. <c>ProductId</c> is set for Current, Moved and Gone; <c>CurrentPath</c> for Current and Moved.</summary>
public sealed record PathResolutionResult(PathOutcome Outcome, long? ProductId, string? CurrentPath);

/// <summary>
/// The Catalog contract the storefront (Sprint 9) uses to resolve a public path: one indexed read, redirect chains already collapsed.
/// Only published products resolve as Current or Moved; an unpublished or draft product reads NotFound to the public.
/// </summary>
public interface IProductPathQueries
{
    Task<PathResolutionResult> ResolveAsync(string path, CancellationToken cancellationToken);
}
