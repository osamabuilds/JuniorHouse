using FluentValidation;
using MediatR;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// Puts a product on sale (PRD-4, PRD-10, PRD-13). The publish gate runs first and returns every failure at once; nothing changes if
/// any fail. A product with some variants still waiting for a GTIN publishes: those variants cannot be bought until Labelling issues
/// theirs (D1). Publishing writes the status, the path history and <c>ProductPublishedEvent</c> in one transaction. Publishing a
/// product that is already published returns it unchanged.
/// </summary>
public sealed record PublishProductCommand(long ProductId, uint Version, string ActingPerson) : IRequest<PublishProductResult>, ICatalogCommand;

public sealed class PublishProductCommandValidator : AbstractValidator<PublishProductCommand>
{
    public PublishProductCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class PublishProductCommandHandler(
    ICatalogDbContext dbContext, ProductAssembler assembler, ProductPathWriter pathWriter, IProductPathBuilder paths, TimeProvider timeProvider)
    : IRequestHandler<PublishProductCommand, PublishProductResult>
{
    public async Task<PublishProductResult> Handle(PublishProductCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        if (product.Status == ProductStatus.Published)
        {
            return new PublishProductResult(true, await assembler.BuildAsync(product, cancellationToken), []);
        }

        dbContext.ExpectVersion(product, request.Version);
        var views = await assembler.JoinVariantsAsync(product, cancellationToken);
        var gate = await assembler.EvaluateGateAsync(product, views, cancellationToken);
        if (!gate.Passed)
        {
            return new PublishProductResult(
                false,
                await assembler.BuildAsync(product, cancellationToken),
                gate.Failures.Select(f => new GateFailureDto(f.Code, f.Message, f.Tab)).ToList());
        }

        var path = await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken);
        product.Publish(request.ActingPerson.Trim(), timeProvider.GetUtcNow(), path);
        await pathWriter.EnsureHistoryAsync(product, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PublishProductResult(true, await assembler.BuildAsync(product, cancellationToken), []);
    }
}

/// <summary>Takes a published product off sale without deleting anything (PRD-3). It stays in order history.</summary>
public sealed record UnpublishProductCommand(long ProductId, uint Version, string ActingPerson) : IRequest<ProductDto>, ICatalogCommand;

public sealed class UnpublishProductCommandValidator : AbstractValidator<UnpublishProductCommand>
{
    public UnpublishProductCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class UnpublishProductCommandHandler(ICatalogDbContext dbContext, ProductAssembler assembler, IProductPathBuilder paths)
    : IRequestHandler<UnpublishProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(UnpublishProductCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        dbContext.ExpectVersion(product, request.Version);
        product.Unpublish(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await assembler.BuildAsync(product, cancellationToken);
    }
}

/// <summary>
/// Retires a product for good (PRD-6): it is archived, never deleted, so orders keep their history. Its address answers "gone".
/// Archiving an archived product changes nothing.
/// </summary>
public sealed record ArchiveProductCommand(long ProductId, uint Version, string ActingPerson) : IRequest<ProductDto>, ICatalogCommand;

public sealed class ArchiveProductCommandValidator : AbstractValidator<ArchiveProductCommand>
{
    public ArchiveProductCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class ArchiveProductCommandHandler(ICatalogDbContext dbContext, ProductAssembler assembler, ProductPathWriter pathWriter, IProductPathBuilder paths)
    : IRequestHandler<ArchiveProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        if (product.Status == ProductStatus.Archived)
        {
            return await assembler.BuildAsync(product, cancellationToken);
        }

        dbContext.ExpectVersion(product, request.Version);

        // The address must keep answering "gone" (410), so it is in the history even if the product was never published.
        await pathWriter.EnsureHistoryAsync(product, cancellationToken);
        product.Archive(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await assembler.BuildAsync(product, cancellationToken);
    }
}
