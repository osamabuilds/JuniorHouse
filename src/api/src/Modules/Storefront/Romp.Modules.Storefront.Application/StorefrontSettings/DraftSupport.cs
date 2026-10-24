using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// What every save command shares: load the whole working copy as a <see cref="SettingsContent"/>, swap in the part being saved,
/// validate that part (ST-4) and check the draft version (ST-14). The sets are tiny (tens of rows), so a part is replaced as a whole.
/// </summary>
internal static class DraftSupport
{
    /// <summary>Loads the draft in a constant number of queries (no N+1). The entities are tracked, so a handler can remove the part it replaces.</summary>
    public static async Task<LoadedDraft> LoadAsync(IStorefrontDbContext db, IExternalHostPolicy hosts, CancellationToken cancellationToken)
    {
        var navItems = await db.NavItems.ToListAsync(cancellationToken); // relationship fix-up fills every Children list
        var roots = navItems.Where(n => !navItems.Any(p => p.Children.Contains(n))).OrderBy(n => n.SortSeq).ToList();
        var ages = await db.AgeSteps.OrderBy(a => a.SortSeq).ToListAsync(cancellationToken);
        var footer = await db.FooterColumns.Include(c => c.Links).OrderBy(c => c.SortSeq).ToListAsync(cancellationToken);
        var items = await db.SettingItems.OrderBy(i => i.Key).ToListAsync(cancellationToken);
        var searches = await db.PopularSearches.OrderBy(s => s.SortSeq).ToListAsync(cancellationToken);
        var delivery = await db.DeliveryRules.OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var exchange = await db.ExchangePolicies.Include(p => p.Conditions).FirstOrDefaultAsync(cancellationToken);
        var pages = await db.Pages.Include(p => p.Blocks).ThenInclude(b => b.Items).AsSplitQuery().OrderBy(p => p.Id).ToListAsync(cancellationToken);
        var draft = await db.Drafts.SingleAsync(cancellationToken);

        var content = new SettingsContent(roots, ages, footer, items, delivery, exchange, pages, [.. searches.Select(s => s.Text)], hosts.AllowedHosts)
        {
            PagesMustBePublished = false,
        };
        return new LoadedDraft(draft, content, navItems, searches);
    }

    /// <summary>Throws a field-level <see cref="ValidationException"/> for the problems that belong to the part just edited (ST-4).</summary>
    public static void ThrowIfInvalid(SettingsContent content, params string[] fieldPrefixes)
    {
        var problems = SettingsValidator.Validate(content)
            .Where(p => fieldPrefixes.Any(prefix => p.Field == prefix || p.Field.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToList();
        ThrowIfAny(problems);
    }

    public static void ThrowIfAny(IReadOnlyCollection<SettingsProblem> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        throw new ValidationException(problems
            .GroupBy(p => p.Field)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Message).Distinct().ToArray()));
    }
}

/// <summary>The working copy as loaded: the version counter, the content for validation, and the rows a replace has to remove.</summary>
internal sealed record LoadedDraft(SettingsDraft Draft, SettingsContent Content, List<NavItem> AllNavItems, List<PopularSearch> PopularSearchRows);
