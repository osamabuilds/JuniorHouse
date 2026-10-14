using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Contracts;

namespace Romp.Modules.Catalog.Infrastructure.Urls;

/// <summary>The Catalog side of <see cref="IProductPathQueries"/> (ADR 0013): the storefront resolves a public path in-process through this.</summary>
public sealed class ProductPathQueries(PathResolver resolver) : IProductPathQueries
{
    public Task<PathResolutionResult> ResolveAsync(string path, CancellationToken cancellationToken) =>
        resolver.ResolveAsync(path, cancellationToken);
}
