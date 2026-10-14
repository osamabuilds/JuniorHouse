using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Labelling.Contracts;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// Brings Catalog's copy of one Labelling variant up to date (PRD-13..15, ADR 0014). Sent by the consumers of
/// <c>VariantGtinAssignedEvent</c> and <c>GtinVoidedEvent</c>. It reads Labelling's current facts through the contract and upserts them
/// by Labelling variant id, so an event delivered twice, or two events in the wrong order, give the same rows as one in order. When
/// Labelling cannot answer (the variant is gone) the event's own facts are used. A published product whose variants changed tells the
/// storefront (<c>ProductChangedEvent</c>).
/// </summary>
public sealed record ApplyLabellingVariantCommand(LabelledVariantInfo Fallback) : IRequest<bool>, ICatalogCommand;

public sealed class ApplyLabellingVariantCommandValidator : AbstractValidator<ApplyLabellingVariantCommand>
{
    public ApplyLabellingVariantCommandValidator()
    {
        RuleFor(c => c.Fallback.VariantId).GreaterThan(0);
        RuleFor(c => c.Fallback.StyleId).GreaterThan(0);
        RuleFor(c => c.Fallback.SkuCode).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Fallback.Gtin).NotEmpty().Length(13);
    }
}

public sealed class ApplyLabellingVariantCommandHandler(
    ICatalogDbContext dbContext, IVariantQueries labelling, LabelledVariantWriter writer, IProductPathBuilder paths)
    : IRequestHandler<ApplyLabellingVariantCommand, bool>
{
    public async Task<bool> Handle(ApplyLabellingVariantCommand request, CancellationToken cancellationToken)
    {
        var current = await labelling.GetByIdAsync(request.Fallback.VariantId, cancellationToken) ?? request.Fallback;
        var changedStyles = await writer.UpsertAsync([current], cancellationToken);
        if (changedStyles.Count == 0)
        {
            return false;
        }

        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.StyleId == current.StyleId, cancellationToken);
        if (product is { Status: ProductStatus.Published })
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Re-reads every variant of the product's style from Labelling (PRD-15): the manual catch-up when events were missed. Gives the same
/// rows as the events would have; running it twice changes nothing.
/// </summary>
public sealed record RefreshVariantsFromLabellingCommand(long ProductId) : IRequest<ProductDto>, ICatalogCommand;

public sealed class RefreshVariantsFromLabellingCommandHandler(
    ICatalogDbContext dbContext, IVariantQueries labelling, LabelledVariantWriter writer, ProductAssembler assembler, IProductPathBuilder paths)
    : IRequestHandler<RefreshVariantsFromLabellingCommand, ProductDto>
{
    public async Task<ProductDto> Handle(RefreshVariantsFromLabellingCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        var changed = await writer.UpsertAsync(await labelling.GetByStyleAsync(product.StyleId, cancellationToken), cancellationToken);
        if (changed.Count > 0)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await assembler.BuildAsync(product, cancellationToken);
    }
}
