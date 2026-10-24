using System.Text.RegularExpressions;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>Everything the validator looks at: the whole draft, as plain collections.</summary>
public sealed record SettingsContent(
    IReadOnlyList<NavItem> Navigation,
    IReadOnlyList<AgeStep> AgeSteps,
    IReadOnlyList<FooterColumn> Footer,
    IReadOnlyList<SettingItem> Items,
    IReadOnlyList<DeliveryRule> DeliveryRules,
    ExchangePolicy? Exchange,
    IReadOnlyList<Page> Pages,
    IReadOnlyList<string> PopularSearches,
    IReadOnlyCollection<string> AllowedExternalHosts)
{
    /// <summary>True once the Catalog category tree exists (SCRUM-207); until then a category target is refused.</summary>
    public bool AllowCategoryTargets { get; init; }

    /// <summary>True (the default) when the whole draft is checked for publishing; saving a part sets it false so links to draft pages are allowed.</summary>
    public bool PagesMustBePublished { get; init; } = true;
}

/// <summary>The facts a page may quote, and whether each is filled in yet (ST-17).</summary>
public static class SettingsFacts
{
    public const string ContactWhatsapp = "contact.whatsapp";
    public const string ContactEmail = "contact.email";
    public const string ContactHours = "contact.hours";
    public const string ExchangeWindow = "exchange.window";
    public const string ExchangeConditions = "exchange.conditions";
    public const string DeliveryRules = "delivery.rules";

    public static readonly IReadOnlyList<string> Keys =
        [ContactWhatsapp, ContactEmail, ContactHours, ExchangeWindow, ExchangeConditions, DeliveryRules];

    /// <summary>The <c>FACT_LKP</c> row id of a fact key (its position in <see cref="Keys"/>, from 1); 0 for an unknown key.</summary>
    public static short IdOf(string key) => (short)(Keys.ToList().IndexOf(key) + 1);

    public static string? KeyOf(short id) => id >= 1 && id <= Keys.Count ? Keys[id - 1] : null;

    public static bool IsFilled(SettingsContent content, string key) => key switch
    {
        ContactWhatsapp => HasItem(content, SettingKey.Whatsapp),
        ContactEmail => HasItem(content, SettingKey.Email),
        ContactHours => HasItem(content, SettingKey.Hours),
        ExchangeWindow => content.Exchange is { WindowDays: > 0 },
        ExchangeConditions => content.Exchange is { Conditions.Count: > 0 },
        DeliveryRules => content.DeliveryRules.Any(r => r.IsEnabled),
        _ => false,
    };

    private static bool HasItem(SettingsContent content, SettingKey key) =>
        content.Items.Any(i => i.Key == key && i.Value.Length > 0);
}

/// <summary>
/// Validates the whole draft and collects every problem, each naming the field and what to do (ST-4). Used by the save commands for the
/// part they change and by publish for the whole document (ST-3, ST-17).
/// </summary>
public static partial class SettingsValidator
{
    [GeneratedRegex("^[a-z0-9-]{2,40}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z0-9-]{1,20}$")]
    private static partial Regex AgeCodePattern();

    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$")]
    private static partial Regex E164Pattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    public static IReadOnlyList<SettingsProblem> Validate(SettingsContent content)
    {
        var problems = new List<SettingsProblem>();
        ValidateNavigation(content, problems);
        ValidateAgeSteps(content, problems);
        ValidateFooter(content, problems);
        ValidateItems(content, problems);
        ValidateDelivery(content, problems);
        ValidatePopularSearches(content, problems);
        ValidatePages(content, problems);
        return problems;
    }

    private static void ValidateNavigation(SettingsContent content, List<SettingsProblem> problems)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        var hasEnabledLink = false;
        var publishedSlugs = UsablePageSlugs(content);

        void Walk(IReadOnlyList<NavItem> siblings, int level, string path)
        {
            var sorts = new HashSet<int>();
            foreach (var item in siblings)
            {
                count++;
                var field = $"navigation.{path}{item.Key}";

                if (level > SettingsLimits.MaxNavDepth)
                {
                    problems.Add(new SettingsProblem(field, $"\"{item.Label}\" is on level {level}. Only {SettingsLimits.MaxNavDepth} levels are allowed. Move it up a level."));
                    continue;
                }

                if (!KeyPattern().IsMatch(item.Key))
                {
                    problems.Add(new SettingsProblem(field, "Enter a key of 2 to 40 lowercase letters, digits or hyphens."));
                }
                else if (!keys.Add(item.Key))
                {
                    problems.Add(new SettingsProblem(field, $"The key \"{item.Key}\" is used twice. Give each entry its own key."));
                }

                if (item.Label.Length is 0 or > SettingsLimits.MaxLabelLength)
                {
                    problems.Add(new SettingsProblem(field, $"Enter a label of 1 to {SettingsLimits.MaxLabelLength} characters."));
                }

                if (!sorts.Add(item.SortSeq))
                {
                    problems.Add(new SettingsProblem(field, $"Two entries at the same level share position {item.SortSeq}. Give each its own position."));
                }

                if (item.Kind == NavKind.Link)
                {
                    if (item.Target is null)
                    {
                        problems.Add(new SettingsProblem(field, $"Choose where \"{item.Label}\" links to."));
                    }
                    else
                    {
                        CheckTarget(field, item.Target, content, publishedSlugs, problems);
                        if (item.IsEnabled)
                        {
                            hasEnabledLink = true;
                        }
                    }
                }

                if (item.Kind == NavKind.Group && !item.Children.Any(c => c.IsEnabled))
                {
                    problems.Add(new SettingsProblem(field, $"\"{item.Label}\" is a group with nothing in it. Add an entry or disable the group."));
                }

                Walk(item.Children, level + 1, $"{path}{item.Key}.");
            }
        }

        Walk(content.Navigation, 1, string.Empty);

        if (count > SettingsLimits.MaxNavItems)
        {
            problems.Add(new SettingsProblem("navigation", $"The menu has {count} entries. At most {SettingsLimits.MaxNavItems} are allowed."));
        }

        if (!hasEnabledLink && !content.Navigation.Any(n => n.IsEnabled && n.Kind == NavKind.CollectionList))
        {
            problems.Add(new SettingsProblem("navigation", "The menu has no links. Add at least one enabled link."));
        }
    }

    /// <summary>The pages a link may point at: published ones when publishing, any page that is not archived when saving a draft.</summary>
    private static HashSet<string> UsablePageSlugs(SettingsContent content) =>
        content.Pages
            .Where(p => content.PagesMustBePublished ? p.Status == PageStatus.Published : p.Status != PageStatus.Archived)
            .Select(p => p.Slug)
            .ToHashSet(StringComparer.Ordinal);

    private static void CheckTarget(string field, LinkTarget target, SettingsContent content, HashSet<string> publishedSlugs, List<SettingsProblem> problems)
    {
        var problem = LinkTarget.Check(target.Kind, target.Reference, content.AllowedExternalHosts, content.AllowCategoryTargets);
        if (problem is not null)
        {
            problems.Add(new SettingsProblem(field, problem));
        }
        else if (target.Kind == LinkTargetKind.Page && !publishedSlugs.Contains(target.Reference.Trim()))
        {
            problems.Add(new SettingsProblem(field, $"The page \"{target.Reference}\" does not exist or is not published. Publish it or choose another page."));
        }
    }

    private static void ValidateAgeSteps(SettingsContent content, List<SettingsProblem> problems)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in content.AgeSteps)
        {
            var field = $"ages.{step.Code}";
            if (!AgeCodePattern().IsMatch(step.Code))
            {
                problems.Add(new SettingsProblem(field, "Enter a code of 1 to 20 lowercase letters, digits or hyphens."));
            }
            else if (!codes.Add(step.Code))
            {
                problems.Add(new SettingsProblem(field, $"The age code \"{step.Code}\" is used twice."));
            }

            if (step.Label.Length is 0 or > SettingsLimits.MaxLabelLength)
            {
                problems.Add(new SettingsProblem(field, $"Enter a label of 1 to {SettingsLimits.MaxLabelLength} characters."));
            }
        }
    }

    private static void ValidateFooter(SettingsContent content, List<SettingsProblem> problems)
    {
        var publishedSlugs = UsablePageSlugs(content);
        foreach (var column in content.Footer)
        {
            if (column.Title.Length is 0 or > SettingsLimits.MaxLabelLength)
            {
                problems.Add(new SettingsProblem($"footer.{column.Title}", $"Enter a column title of 1 to {SettingsLimits.MaxLabelLength} characters."));
            }

            foreach (var link in column.Links)
            {
                var field = $"footer.{column.Title}.{link.Label}";
                if (link.Label.Length is 0 or > SettingsLimits.MaxLabelLength)
                {
                    problems.Add(new SettingsProblem(field, $"Enter a link label of 1 to {SettingsLimits.MaxLabelLength} characters."));
                }

                CheckTarget(field, link.Target, content, publishedSlugs, problems);
            }
        }
    }

    private static void ValidateItems(SettingsContent content, List<SettingsProblem> problems)
    {
        foreach (var item in content.Items)
        {
            var field = $"settings.{item.Key}";
            switch (item.Key)
            {
                case SettingKey.Whatsapp when item.Value.Length > 0 && !E164Pattern().IsMatch(item.Value):
                    problems.Add(new SettingsProblem(field, "Enter the number with the country code and no spaces, for example +923001234567."));
                    break;
                case SettingKey.Email when item.Value.Length > 0 && !EmailPattern().IsMatch(item.Value):
                    problems.Add(new SettingsProblem(field, "Enter a valid email address, for example hello@example.com."));
                    break;
                case SettingKey.BrandName when item.Value.Length is 0 or > 60:
                    problems.Add(new SettingsProblem(field, "Enter the brand name, up to 60 characters."));
                    break;
                case SettingKey.Announcement when item.Value.Length > 200:
                    problems.Add(new SettingsProblem(field, "Keep the announcement to 200 characters or fewer."));
                    break;
                case SettingKey.Tagline or SettingKey.Hours when item.Value.Length > 120:
                    problems.Add(new SettingsProblem(field, "Keep this to 120 characters or fewer."));
                    break;
            }

            if (item.Value.Any(char.IsControl))
            {
                problems.Add(new SettingsProblem(field, "Remove the line breaks and control characters."));
            }
        }
    }

    private static void ValidateDelivery(SettingsContent content, List<SettingsProblem> problems)
    {
        foreach (var rule in content.DeliveryRules)
        {
            var field = $"delivery.{rule.City}";
            if (rule.City.Length == 0)
            {
                problems.Add(new SettingsProblem(field, "Enter the city."));
            }

            if (rule.MinDays < 0 || rule.MaxDays < rule.MinDays)
            {
                problems.Add(new SettingsProblem(field, "The longest delivery time must be at least the shortest, and neither can be negative."));
            }

            if (rule.ChargeAmt < 0)
            {
                problems.Add(new SettingsProblem(field, "The delivery charge cannot be negative."));
            }

            if (rule.FreeThresholdAmt is < 0)
            {
                problems.Add(new SettingsProblem(field, "The free-delivery amount cannot be negative."));
            }
        }

        if (content.Exchange is { } exchange && (exchange.WindowDays < 1 || exchange.WindowDays > 365))
        {
            problems.Add(new SettingsProblem("exchange.window", "Enter an exchange window of 1 to 365 days."));
        }
    }

    private static void ValidatePopularSearches(SettingsContent content, List<SettingsProblem> problems)
    {
        if (content.PopularSearches.Count > SettingsLimits.MaxPopularSearches)
        {
            problems.Add(new SettingsProblem("popularSearches", $"Use at most {SettingsLimits.MaxPopularSearches} popular searches."));
        }
    }

    private static void ValidatePages(SettingsContent content, List<SettingsProblem> problems)
    {
        if (content.Pages.Count > SettingsLimits.MaxPages)
        {
            problems.Add(new SettingsProblem("pages", $"There are {content.Pages.Count} pages. At most {SettingsLimits.MaxPages} are allowed."));
        }

        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in content.Pages)
        {
            var field = $"pages.{page.Slug}";
            var slugProblem = Page.CheckSlug(page.Slug);
            if (slugProblem is not null)
            {
                problems.Add(new SettingsProblem(field, slugProblem));
            }
            else if (!slugs.Add(page.Slug))
            {
                problems.Add(new SettingsProblem(field, $"The page address \"{page.Slug}\" is used twice."));
            }

            if (page.MetaDescription.Length > SettingsLimits.MaxMetaDescriptionLength)
            {
                problems.Add(new SettingsProblem(field, $"Keep the search description to {SettingsLimits.MaxMetaDescriptionLength} characters or fewer."));
            }

            ValidateBlocks(page, content, field, problems);

            if (page.Status != PageStatus.Published)
            {
                continue;
            }

            if (page.Title.Length == 0)
            {
                problems.Add(new SettingsProblem(field, "Enter a page title before publishing."));
            }

            if (page.MetaDescription.Length == 0)
            {
                problems.Add(new SettingsProblem(field, "Enter a search description before publishing."));
            }

            foreach (var block in page.Blocks.Where(b => b.Type == BlockType.Fact && b.FactKey is not null && SettingsFacts.Keys.Contains(b.FactKey)))
            {
                if (!SettingsFacts.IsFilled(content, block.FactKey!))
                {
                    problems.Add(new SettingsProblem(field, $"The page quotes \"{block.FactKey}\", which is still empty. Fill it in under Settings before publishing the page."));
                }
            }
        }
    }

    private static void ValidateBlocks(Page page, SettingsContent content, string field, List<SettingsProblem> problems)
    {
        if (page.Blocks.Count > SettingsLimits.MaxBlocksPerPage)
        {
            problems.Add(new SettingsProblem(field, $"A page can have at most {SettingsLimits.MaxBlocksPerPage} blocks."));
        }

        var firstHeading = page.Blocks.FirstOrDefault(b => b.Type == BlockType.Heading);
        if (firstHeading is { Level: not 2 })
        {
            problems.Add(new SettingsProblem(field, "The first heading must be a level 2 heading."));
        }

        foreach (var block in page.Blocks)
        {
            switch (block.Type)
            {
                case BlockType.Heading when block.Level is not (2 or 3):
                    problems.Add(new SettingsProblem(field, "A heading must be level 2 or level 3."));
                    break;
                case BlockType.Paragraph or BlockType.Callout when block.Text.Length > SettingsLimits.MaxParagraphLength:
                    problems.Add(new SettingsProblem(field, $"A paragraph can have at most {SettingsLimits.MaxParagraphLength} characters."));
                    break;
                case BlockType.Fact when block.FactKey is null || !SettingsFacts.Keys.Contains(block.FactKey):
                    problems.Add(new SettingsProblem(field, $"Choose one of the known facts for the fact block ({string.Join(", ", SettingsFacts.Keys)})."));
                    break;
            }
        }
    }
}
