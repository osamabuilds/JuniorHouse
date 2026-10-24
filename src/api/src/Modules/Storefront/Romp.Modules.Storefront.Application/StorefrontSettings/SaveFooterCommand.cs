using FluentValidation;
using MediatR;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>Saves the footer columns and links. Every link target is typed and checked (ST-1, ST-4).</summary>
public sealed record SaveFooterCommand(int ExpectedVersion, IReadOnlyList<FooterColumnDto> Columns)
    : IRequest<SavedDto<IReadOnlyList<FooterColumnDto>>>, IStorefrontCommand;

public sealed class SaveFooterCommandValidator : AbstractValidator<SaveFooterCommand>
{
    public SaveFooterCommandValidator() => RuleFor(c => c.Columns).NotNull();
}

public sealed class SaveFooterCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts)
    : IRequestHandler<SaveFooterCommand, SavedDto<IReadOnlyList<FooterColumnDto>>>
{
    public async Task<SavedDto<IReadOnlyList<FooterColumnDto>>> Handle(SaveFooterCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        loaded.Draft.Advance(request.ExpectedVersion);

        var columns = request.Columns.Select(SettingsMapper.ToEntity).OrderBy(c => c.SortSeq).ToList();
        DraftSupport.ThrowIfInvalid(loaded.Content with { Footer = columns }, "footer");

        db.FooterColumns.RemoveRange(loaded.Content.Footer);
        db.FooterColumns.AddRange(columns);
        await db.SaveChangesAsync(cancellationToken);

        return new SavedDto<IReadOnlyList<FooterColumnDto>>(loaded.Draft.Version, [.. columns.Select(SettingsMapper.ToDto)]);
    }
}
