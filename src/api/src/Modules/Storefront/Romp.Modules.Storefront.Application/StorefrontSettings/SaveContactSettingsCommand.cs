using FluentValidation;
using MediatR;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// Saves the brand, contact and announcement values and the popular-search chips (the "Settings" screen). WhatsApp must be in
/// international format, email must be an address (ST-4).
/// </summary>
public sealed record SaveContactSettingsCommand(int ExpectedVersion, IReadOnlyList<SettingItemDto> Items, IReadOnlyList<string> PopularSearches)
    : IRequest<SavedDto<ContactSettingsDto>>, IStorefrontCommand;

public sealed record ContactSettingsDto(IReadOnlyList<SettingItemDto> Items, IReadOnlyList<string> PopularSearches);

public sealed class SaveContactSettingsCommandValidator : AbstractValidator<SaveContactSettingsCommand>
{
    public SaveContactSettingsCommandValidator()
    {
        RuleFor(c => c.Items).NotNull();
        RuleFor(c => c.PopularSearches).NotNull();
    }
}

public sealed class SaveContactSettingsCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts)
    : IRequestHandler<SaveContactSettingsCommand, SavedDto<ContactSettingsDto>>
{
    public async Task<SavedDto<ContactSettingsDto>> Handle(SaveContactSettingsCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        loaded.Draft.Advance(request.ExpectedVersion);

        var items = request.Items.Select(i => new SettingItem(i.Key, i.Value)).ToList();
        var terms = request.PopularSearches.Select(t => (t ?? string.Empty).Trim()).ToList();

        var problems = new List<SettingsProblem>();
        problems.AddRange(items.GroupBy(i => i.Key).Where(g => g.Count() > 1)
            .Select(g => new SettingsProblem($"settings.{g.Key}", $"{g.Key} is entered twice. Keep one value.")));
        problems.AddRange(terms.Where(t => t.Length is 0 or > 40)
            .Select(t => new SettingsProblem("popularSearches", "Each popular search must be 1 to 40 characters.")));
        problems.AddRange(terms.GroupBy(t => t, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => new SettingsProblem("popularSearches", $"\"{g.Key}\" is listed twice.")));
        DraftSupport.ThrowIfAny(problems);

        var content = loaded.Content with { Items = items, PopularSearches = terms };
        DraftSupport.ThrowIfInvalid(content, "settings", "popularSearches");

        db.SettingItems.RemoveRange(loaded.Content.Items);
        db.PopularSearches.RemoveRange(loaded.PopularSearchRows);
        await db.SaveChangesAsync(cancellationToken);
        db.SettingItems.AddRange(items);
        db.PopularSearches.AddRange(terms.Select((t, i) => new PopularSearch(t, i + 1)));
        await db.SaveChangesAsync(cancellationToken);

        return new SavedDto<ContactSettingsDto>(
            loaded.Draft.Version,
            new ContactSettingsDto([.. items.Select(i => new SettingItemDto(i.Key, i.Value))], terms));
    }
}
