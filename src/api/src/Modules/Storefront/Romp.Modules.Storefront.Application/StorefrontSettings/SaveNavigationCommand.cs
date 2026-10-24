using FluentValidation;
using MediatR;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// Saves the whole navigation tree (up to three levels). The tree is checked as a whole: depth, unique keys, unique positions among
/// siblings, groups with members, typed targets, dead links (ST-1, ST-1a, ST-4). A stale <see cref="ExpectedVersion"/> is refused (ST-14).
/// </summary>
public sealed record SaveNavigationCommand(int ExpectedVersion, IReadOnlyList<NavItemDto> Items)
    : IRequest<SavedDto<IReadOnlyList<NavItemDto>>>, IStorefrontCommand;

public sealed class SaveNavigationCommandValidator : AbstractValidator<SaveNavigationCommand>
{
    public SaveNavigationCommandValidator() => RuleFor(c => c.Items).NotNull();
}

public sealed class SaveNavigationCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts)
    : IRequestHandler<SaveNavigationCommand, SavedDto<IReadOnlyList<NavItemDto>>>
{
    public async Task<SavedDto<IReadOnlyList<NavItemDto>>> Handle(SaveNavigationCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        loaded.Draft.Advance(request.ExpectedVersion);

        var roots = request.Items.Select(SettingsMapper.ToEntity).OrderBy(n => n.SortSeq).ToList();
        DraftSupport.ThrowIfInvalid(loaded.Content with { Navigation = roots }, "navigation");

        // ITEM_KEY is unique, so the old rows go first and the new tree is written after them, in the same transaction.
        db.NavItems.RemoveRange(loaded.AllNavItems);
        await db.SaveChangesAsync(cancellationToken);
        db.NavItems.AddRange(roots);
        await db.SaveChangesAsync(cancellationToken);

        return new SavedDto<IReadOnlyList<NavItemDto>>(loaded.Draft.Version, [.. roots.Select(SettingsMapper.ToDto)]);
    }
}
