using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Urls;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Urls;

/// <summary>A suggested slug for a product name within a gender and category (URL-4).</summary>
public sealed record SlugSuggestionDto(string Slug, string Path);

/// <summary>
/// Suggests the first free slug for a product name: the normalised name, else the name with <c>-2</c>, <c>-3</c>, ... (URL-4, URL-6).
/// Free means not used by another product's current slug and not in any product's history (URL-11). One query reads every slug in
/// the gender and category that starts with the base.
/// </summary>
public sealed record SuggestSlugQuery(string Name, short GenderId, short CategoryId, long? ProductId = null) : IRequest<SlugSuggestionDto>;

public sealed class SuggestSlugQueryValidator : AbstractValidator<SuggestSlugQuery>
{
    public SuggestSlugQueryValidator()
    {
        RuleFor(q => q.Name).NotEmpty().MaximumLength(200);
        RuleFor(q => q.GenderId).GreaterThan((short)0);
        RuleFor(q => q.CategoryId).GreaterThan((short)0);
    }
}

public sealed class SuggestSlugQueryHandler(ICatalogDbContext dbContext, IProductPathBuilder paths, IOptions<CatalogOptions> options)
    : IRequestHandler<SuggestSlugQuery, SlugSuggestionDto>
{
    public async Task<SlugSuggestionDto> Handle(SuggestSlugQuery request, CancellationToken cancellationToken)
    {
        var slug = await new SlugSuggester(dbContext, options.Value).FirstFreeAsync(request.Name, request.GenderId, request.CategoryId, request.ProductId, cancellationToken);
        return new SlugSuggestionDto(slug, await paths.PathOfAsync(request.GenderId, request.CategoryId, slug, cancellationToken));
    }
}

/// <summary>The suggestion rule, shared by the query and by product creation.</summary>
public sealed class SlugSuggester(ICatalogDbContext dbContext, CatalogOptions options)
{
    public async Task<string> FirstFreeAsync(string name, short genderId, short categoryId, long? ownProductId, CancellationToken cancellationToken)
    {
        var baseSlug = SlugPolicy.Normalise(name, options.MaxSlugLength);
        if (baseSlug.Length == 0)
        {
            baseSlug = "product";
        }

        var prefix = baseSlug + "-";
        var taken = (await dbContext.Products.AsNoTracking()
                .Where(p => p.GenderId == genderId && p.CategoryId == categoryId && p.Id != ownProductId && (p.Slug == baseSlug || p.Slug.StartsWith(prefix)))
                .Select(p => p.Slug)
                .Union(dbContext.PathHistory.AsNoTracking()
                    .Where(h => h.GenderId == genderId && h.CategoryId == categoryId && h.ProductId != ownProductId && (h.Slug == baseSlug || h.Slug.StartsWith(prefix)))
                    .Select(h => h.Slug))
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var reserved = options.ReservedSlugs;
        var candidate = baseSlug;
        for (var n = 2; taken.Contains(candidate) || reserved.Contains(candidate, StringComparer.Ordinal); n++)
        {
            candidate = SlugPolicy.WithSuffix(baseSlug, n, options.MaxSlugLength);
        }

        return candidate;
    }
}

/// <summary>One path a product has had.</summary>
public sealed record PathHistoryDto(string Path, bool IsCurrent, DateTimeOffset FirstUsedAt);

/// <summary>The paths a product has had, newest first. Every one of them redirects to the current path (URL-2, URL-3).</summary>
public sealed record GetPathHistoryQuery(long ProductId) : IRequest<IReadOnlyList<PathHistoryDto>>;

public sealed class GetPathHistoryQueryHandler(ICatalogDbContext dbContext, IProductPathBuilder paths)
    : IRequestHandler<GetPathHistoryQuery, IReadOnlyList<PathHistoryDto>>
{
    public async Task<IReadOnlyList<PathHistoryDto>> Handle(GetPathHistoryQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.GenderId, p.CategoryId, p.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");

        var rows = await dbContext.PathHistory.AsNoTracking()
            .Where(h => h.ProductId == request.ProductId)
            .OrderByDescending(h => h.Id)
            .Select(h => new { h.GenderId, h.CategoryId, h.Slug, h.InsrDte })
            .ToListAsync(cancellationToken);

        var map = await paths.LoadAsync(
            rows.Select(r => r.GenderId).Append(product.GenderId).Distinct().ToList(),
            rows.Select(r => r.CategoryId).Append(product.CategoryId).Distinct().ToList(),
            cancellationToken);

        return rows
            .Select(r => new PathHistoryDto(
                map.PathOf(r.GenderId, r.CategoryId, r.Slug) ?? string.Empty,
                r.GenderId == product.GenderId && r.CategoryId == product.CategoryId && r.Slug == product.Slug,
                r.InsrDte))
            .ToList();
    }
}

/// <summary>Resolves a requested path (URL-2, URL-5, URL-9) the way the storefront will: current, moved, gone or not found.</summary>
public sealed record ResolvePathQuery(string Path) : IRequest<PathResolutionResult>;

public sealed class ResolvePathQueryValidator : AbstractValidator<ResolvePathQuery>
{
    public ResolvePathQueryValidator() => RuleFor(q => q.Path).NotEmpty().MaximumLength(300);
}

public sealed class ResolvePathQueryHandler(PathResolver resolver) : IRequestHandler<ResolvePathQuery, PathResolutionResult>
{
    public Task<PathResolutionResult> Handle(ResolvePathQuery request, CancellationToken cancellationToken) =>
        resolver.ResolveAsync(request.Path, cancellationToken);
}

/// <summary>
/// The path lookup (ADR 0013): parse, map the two segments to ids, then one indexed read of the path history joined to the product.
/// Only a published product resolves as <see cref="PathOutcome.Current"/> or <see cref="PathOutcome.Moved"/>; an archived one is
/// <see cref="PathOutcome.Gone"/>; everything else, including drafts, is <see cref="PathOutcome.NotFound"/> to the public.
/// </summary>
public sealed class PathResolver(ICatalogDbContext dbContext, ICatalogLookupQueries lookups, IProductPathBuilder paths)
{
    public async Task<PathResolutionResult> ResolveAsync(string requestedPath, CancellationToken cancellationToken)
    {
        var parsed = ProductPath.Parse(ProductPath.Canonicalise(requestedPath));
        if (parsed is null || SlugPolicy.Validate(parsed.Slug, [], 200) is not null)
        {
            return new PathResolutionResult(PathOutcome.NotFound, null, null);
        }

        var genders = await lookups.GetGenderIdsBySegmentAsync([parsed.GenderSegment], cancellationToken);
        var categories = await lookups.GetCategoryIdsBySegmentAsync([parsed.CategorySegment], cancellationToken);
        if (!genders.TryGetValue(parsed.GenderSegment, out var genderId) || !categories.TryGetValue(parsed.CategorySegment, out var categoryId))
        {
            return new PathResolutionResult(PathOutcome.NotFound, null, null);
        }

        var owner = await (from h in dbContext.PathHistory.AsNoTracking()
                           join p in dbContext.Products.AsNoTracking() on h.ProductId equals p.Id
                           where h.GenderId == genderId && h.CategoryId == categoryId && h.Slug == parsed.Slug
                           select new { h.ProductId, p.StatusId, p.GenderId, p.CategoryId, p.Slug })
            .FirstOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            return new PathResolutionResult(PathOutcome.NotFound, null, null);
        }

        if (owner.StatusId == (short)ProductStatus.Archived)
        {
            return new PathResolutionResult(PathOutcome.Gone, owner.ProductId, null);
        }

        if (owner.StatusId != (short)ProductStatus.Published)
        {
            return new PathResolutionResult(PathOutcome.NotFound, null, null);
        }

        var current = await paths.PathOfAsync(owner.GenderId, owner.CategoryId, owner.Slug, cancellationToken);
        var isCurrent = owner.GenderId == genderId && owner.CategoryId == categoryId && owner.Slug == parsed.Slug;
        return new PathResolutionResult(isCurrent ? PathOutcome.Current : PathOutcome.Moved, owner.ProductId, current);
    }
}
