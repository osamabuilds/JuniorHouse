using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.Persistence;

/// <summary>
/// SCRUM-206 task 5, S-1 / S-2: the launch set the migration seeds, read back from a real PostgreSQL, and its agreement with the
/// storefront's built-in launch set (<c>launch-settings.ts</c>, <c>launch-pages.ts</c>) that the site renders until it reads the API.
/// </summary>
public sealed partial class LaunchSeedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private async Task<StfrDbContext> MigratedAsync()
    {
        var context = new StfrDbContext(new DbContextOptionsBuilder<StfrDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
            .Options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static string WebData(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "web")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, "src", "web", "projects", "storefront", "src", "app", "shared", "data", file));
    }

    [GeneratedRegex(@"link\('([a-z0-9-]+)'")]
    private static partial Regex LinkKeys();

    [GeneratedRegex(@"(?:path|page)\('([^']+)'")]
    private static partial Regex FooterLabels();

    [GeneratedRegex(@"\['([a-z-]+)', '([A-Z][^']*)'\]")]
    private static partial Regex PageSlugs();

    [GeneratedRegex(@"(?:text|label|title|description): '((?:[^'\\]|\\.)*)'")]
    private static partial Regex Strings();

    [GeneratedRegex(@"^\s+'((?:[^'\\]|\\.)*)',\r?$", RegexOptions.Multiline)]
    private static partial Regex ListItems();

    [GeneratedRegex(@"key: '([a-z]+\.[a-z]+)'")]
    private static partial Regex FactKeys();

    private static string Unescape(string value) => value.Replace("\\'", "'", StringComparison.Ordinal);

    [Fact]
    [Trait("Spec", "S-1")]
    [Trait("Spec", "ST-1a")]
    public async Task Seed_Navigation_IsTheThreeLevelLaunchTree()
    {
        await using var context = await MigratedAsync();
        var items = await context.NavItems.Include(n => n.Children).ToListAsync();

        var roots = items.Where(i => !items.Any(p => p.Children.Contains(i))).OrderBy(i => i.SortSeq).ToList();
        Assert.Equal(["girls", "boys", "new-in", "collections", "sale"], roots.Select(r => r.Key).ToArray());
        Assert.Equal(NavKind.CollectionList, roots.Single(r => r.Key == "collections").Kind);
        Assert.All(roots.Where(r => r.Key is "girls" or "boys"), r => Assert.True(r.ShowAges));
        Assert.Equal(["girls-dresses", "girls-tops", "girls-bottoms"], roots[0].Children.OrderBy(c => c.SortSeq).Select(c => c.Key).ToArray());
        Assert.Equal(["girls-tees", "girls-blouses"], roots[0].Children.Single(c => c.Key == "girls-tops").Children.OrderBy(c => c.SortSeq).Select(c => c.Key).ToArray());
        Assert.Equal(19, items.Count);
    }

    [Fact]
    [Trait("Spec", "S-2")]
    public async Task Seed_Ages_AreOneToEightYears()
    {
        await using var context = await MigratedAsync();

        var ages = await context.AgeSteps.OrderBy(a => a.SortSeq).ToListAsync();

        Assert.Equal(["1y", "2y", "3y", "4y", "5y", "6y", "7y", "8y"], ages.Select(a => a.Code).ToArray());
        Assert.Equal("1Y", ages[0].Label);
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public async Task Seed_Pages_AreEightDraftsAndEveryQuotedFactIsFilled()
    {
        await using var context = await MigratedAsync();

        var pages = await context.Pages.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(
            ["delivery", "exchanges", "size-guide", "our-story", "made-in-pakistan", "contact", "privacy", "terms"],
            pages.Select(p => p.Slug).ToArray());
        Assert.All(pages, p => Assert.Equal(PageStatus.Draft, p.Status));

        // The email is real; hours, delivery and exchange are placeholders (migration SeedPlaceholderFacts) so every fact a page quotes is filled.
        var settings = await context.SettingItems.ToListAsync();
        Assert.Equal("+923315613217", settings.Single(s => s.Key == SettingKey.Whatsapp).Value);
        Assert.Equal("osamabuss048@gmail.com", settings.Single(s => s.Key == SettingKey.Email).Value);
        Assert.NotEmpty(settings.Single(s => s.Key == SettingKey.Hours).Value);
        Assert.Equal(["Lahore", "Karachi", "Islamabad", "Rest of Pakistan"], (await context.DeliveryRules.OrderBy(r => r.Id).ToListAsync()).Select(r => r.City).ToArray());
        var exchange = await context.ExchangePolicies.Include(p => p.Conditions).SingleAsync();
        Assert.Equal(7, exchange.WindowDays);
        Assert.Equal(3, exchange.Conditions.Count);
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public async Task Seed_AsADraft_FailsPublishOnlyBecauseItsPagesAreNotPublishedYet()
    {
        await using var context = await MigratedAsync();
        var all = await context.NavItems.ToListAsync(); // relationship fix-up fills every Children list
        var content = new SettingsContent(
            all.Where(n => !all.Any(p => p.Children.Contains(n))).OrderBy(n => n.SortSeq).ToList(),
            await context.AgeSteps.ToListAsync(),
            await context.FooterColumns.Include(c => c.Links).ToListAsync(),
            await context.SettingItems.ToListAsync(),
            await context.DeliveryRules.ToListAsync(),
            await context.ExchangePolicies.Include(p => p.Conditions).SingleOrDefaultAsync(),
            await context.Pages.Include(p => p.Blocks).ToListAsync(),
            await context.PopularSearches.Select(p => p.Text).ToListAsync(),
            ["wa.me"]);

        var problems = SettingsValidator.Validate(content);

        Assert.Equal(8, problems.Count);
        Assert.All(problems, p => Assert.Contains("does not exist or is not published", p.Message));
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    public async Task Seed_EqualsTheStorefrontBuiltInLaunchSet()
    {
        await using var context = await MigratedAsync();
        var settingsTs = WebData("launch-settings.ts");
        var pagesTs = WebData("launch-pages.ts");

        // Navigation keys.
        var tsKeys = LinkKeys().Matches(settingsTs).Select(m => m.Groups[1].Value).Append("collections").Order().ToArray();
        Assert.Equal(tsKeys, (await context.NavItems.Select(n => n.Key).ToListAsync()).Order().ToArray());

        // Ages and page slugs.
        Assert.Equal(8, await context.AgeSteps.CountAsync());
        Assert.Contains("length: 8", settingsTs, StringComparison.Ordinal);
        Assert.Equal(
            PageSlugs().Matches(settingsTs).Select(m => m.Groups[1].Value).Order().ToArray(),
            (await context.Pages.Select(p => p.Slug).ToListAsync()).Order().ToArray());

        // Footer link labels (the WhatsApp link is spelled out in the web file).
        var footer = await context.FooterColumns.Include(c => c.Links).ToListAsync();
        Assert.Equal(
            FooterLabels().Matches(settingsTs).Select(m => m.Groups[1].Value).Append("WhatsApp").Order().ToArray(),
            footer.SelectMany(c => c.Links).Select(l => l.Label).Order().ToArray());

        // Every wording string and list item of the pages, and the fact keys they quote.
        var tsTexts = Strings().Matches(pagesTs).Select(m => Unescape(m.Groups[1].Value))
            .Concat(ListItems().Matches(pagesTs).Select(m => Unescape(m.Groups[1].Value)))
            .Where(t => t.Length > 0).ToHashSet();
        var pages = await context.Pages.Include(p => p.Blocks).ThenInclude(b => b.Items).Where(p => p.Slug != "privacy" && p.Slug != "terms").ToListAsync();
        var seededTexts = pages.SelectMany(p => new[] { p.Title, p.MetaDescription })
            .Concat(pages.SelectMany(p => p.Blocks).SelectMany(b => new[] { b.Text, b.Label ?? string.Empty }))
            .Concat(pages.SelectMany(p => p.Blocks).SelectMany(b => b.Items).Select(i => i.Text))
            .Where(t => t.Length > 0).ToHashSet();
        Assert.Empty(tsTexts.Except(seededTexts));
        Assert.Empty(seededTexts.Except(tsTexts));

        Assert.Equal(
            FactKeys().Matches(pagesTs).Select(m => m.Groups[1].Value).Order().ToArray(),
            pages.SelectMany(p => p.Blocks).Where(b => b.FactKey is not null).Select(b => b.FactKey!).Order().ToArray());
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    public async Task Seed_PublishedDocument_MatchesTheCommittedContractFixture()
    {
        await using var context = await MigratedAsync();
        var all = await context.NavItems.ToListAsync();
        var content = new SettingsContent(
            all.Where(n => !all.Any(p => p.Children.Contains(n))).OrderBy(n => n.SortSeq).ToList(),
            await context.AgeSteps.ToListAsync(),
            await context.FooterColumns.Include(c => c.Links).ToListAsync(),
            await context.SettingItems.ToListAsync(),
            await context.DeliveryRules.ToListAsync(),
            await context.ExchangePolicies.Include(p => p.Conditions).SingleOrDefaultAsync(),
            await context.Pages.Include(p => p.Blocks).ThenInclude(b => b.Items).ToListAsync(),
            await context.PopularSearches.Select(p => p.Text).ToListAsync(),
            ["wa.me"]);

        // The preview shows drafts as live, so the document carries all eight pages (the seeded ones are drafts, tasks.md).
        var generated = JsonNode.Parse(SiteDocumentBuilder.Build(content, 0, [], includeDraftPages: true))!;
        var fixturePath = Path.Combine(WebRoot(), "projects", "storefront", "src", "app", "shared", "data", "contract", "api-launch-document.json");

        if (Environment.GetEnvironmentVariable("ROMP_UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
            File.WriteAllText(fixturePath, generated.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        var committed = JsonNode.Parse(File.ReadAllText(fixturePath))!;
        Assert.True(
            JsonNode.DeepEquals(generated, committed),
            "The API document for the seed no longer equals api-launch-document.json. Run this test with ROMP_UPDATE_FIXTURES=1, then run the storefront contract spec.");
    }

    private static string WebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "web")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "web");
    }

    [Fact]
    [Trait("Spec", "ST-16")]
    public async Task Seed_IdentitySequences_ContinuePastTheSeededRows()
    {
        await using var context = await MigratedAsync();
        var page = new Page("shipping", "Shipping", "Shipping.") { InsrDte = DateTimeOffset.UtcNow, InsrBy = "test" };

        context.Pages.Add(page);
        await context.SaveChangesAsync();

        Assert.Equal(9, page.Id);
    }
}
