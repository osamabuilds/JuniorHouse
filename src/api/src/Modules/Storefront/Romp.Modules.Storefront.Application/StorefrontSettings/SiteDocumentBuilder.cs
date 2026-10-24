using System.Text.Json.Nodes;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>One redirect row as the builder needs it: the old slug and the page it now points at.</summary>
public sealed record RedirectRow(string OldSlug, long PageId);

/// <summary>
/// Turns the working copy into the one JSON document a snapshot holds (ADR 0020): <c>settings</c> in the storefront's
/// <c>SiteSettings</c> shape, the content of every published page, the 301s and the addresses that answer 410. The output is
/// deterministic (fixed order), so equal content gives an equal hash and a second publish of it changes nothing.
/// </summary>
public static class SiteDocumentBuilder
{
    /// <param name="includeDraftPages">True for the admin preview: drafts are shown as if published. False for a snapshot.</param>
    public static string Build(SettingsContent content, int versionNo, IReadOnlyList<RedirectRow> redirects, bool includeDraftPages = false)
    {
        var live = content.Pages
            .Where(p => p.Status == PageStatus.Published || (includeDraftPages && p.Status == PageStatus.Draft))
            .OrderBy(p => p.Slug, StringComparer.Ordinal)
            .ToList();
        var archived = content.Pages.Where(p => p.Status == PageStatus.Archived).OrderBy(p => p.Slug, StringComparer.Ordinal).ToList();
        var byId = content.Pages.Where(p => p.Id != 0).ToDictionary(p => p.Id);

        var redirectMap = new JsonObject();
        var gone = new JsonArray();
        foreach (var archivedPage in archived)
        {
            gone.Add(archivedPage.Slug);
        }

        foreach (var redirect in redirects.OrderBy(r => r.OldSlug, StringComparer.Ordinal))
        {
            if (!byId.TryGetValue(redirect.PageId, out var target))
            {
                continue;
            }

            if (target.Status == PageStatus.Archived)
            {
                gone.Add(redirect.OldSlug);
            }
            else if (live.Contains(target))
            {
                redirectMap[redirect.OldSlug] = target.Slug;
            }
        }

        var pages = new JsonObject();
        foreach (var page in live)
        {
            pages[page.Slug] = PageJson(page);
        }

        return new JsonObject
        {
            ["settings"] = SettingsJson(content, versionNo, live, archived),
            ["pages"] = pages,
            ["redirects"] = redirectMap,
            ["gone"] = gone,
        }.ToJsonString();
    }

    private static JsonObject SettingsJson(SettingsContent content, int versionNo, List<Page> live, List<Page> archived)
    {
        string Value(SettingKey key) => content.Items.FirstOrDefault(i => i.Key == key)?.Value ?? string.Empty;

        var brand = new JsonObject
        {
            ["name"] = Value(SettingKey.BrandName),
            ["tagline"] = Value(SettingKey.Tagline),
            ["whatsapp"] = Value(SettingKey.Whatsapp).TrimStart('+'), // the storefront contract carries digits only
        };
        if (Value(SettingKey.Email).Length > 0)
        {
            brand["email"] = Value(SettingKey.Email);
        }

        if (Value(SettingKey.Hours).Length > 0)
        {
            brand["hours"] = Value(SettingKey.Hours);
        }

        var navigation = new JsonArray();
        foreach (var item in content.Navigation.Where(n => n.IsEnabled).OrderBy(n => n.SortSeq))
        {
            navigation.Add(NavJson(item));
        }

        var ages = new JsonArray();
        foreach (var step in content.AgeSteps.Where(a => a.IsEnabled).OrderBy(a => a.SortSeq))
        {
            ages.Add(new JsonObject { ["code"] = step.Code, ["label"] = step.Label, ["sort"] = step.SortSeq, ["enabled"] = true });
        }

        var footer = new JsonArray();
        foreach (var column in content.Footer.OrderBy(c => c.SortSeq))
        {
            var links = new JsonArray();
            foreach (var link in column.Links.Where(l => l.IsEnabled).OrderBy(l => l.SortSeq))
            {
                links.Add(new JsonObject { ["label"] = link.Label, ["target"] = TargetJson(link.Target), ["enabled"] = true });
            }

            if (links.Count > 0)
            {
                footer.Add(new JsonObject { ["title"] = column.Title, ["sort"] = column.SortSeq, ["links"] = links });
            }
        }

        var delivery = new JsonArray();
        foreach (var rule in content.DeliveryRules.Where(r => r.IsEnabled).OrderBy(r => r.City, StringComparer.Ordinal))
        {
            var row = new JsonObject
            {
                ["city"] = rule.City,
                ["minDays"] = rule.MinDays,
                ["maxDays"] = rule.MaxDays,
                ["charge"] = rule.ChargeAmt,
            };
            if (rule.FreeThresholdAmt is { } freeOver)
            {
                row["freeOver"] = freeOver;
            }

            row["courier"] = rule.Courier;
            row["enabled"] = true;
            delivery.Add(row);
        }

        var conditions = new JsonArray();
        foreach (var condition in (content.Exchange?.Conditions ?? []).OrderBy(c => c.SortSeq))
        {
            conditions.Add(condition.Text);
        }

        var summaries = new JsonArray();
        foreach (var page in live.Concat(archived).OrderBy(p => p.Slug, StringComparer.Ordinal))
        {
            summaries.Add(new JsonObject { ["slug"] = page.Slug, ["title"] = page.Title, ["status"] = page.Status == PageStatus.Archived ? "archived" : "published" });
        }

        var searches = new JsonArray();
        foreach (var term in content.PopularSearches)
        {
            searches.Add(term);
        }

        return new JsonObject
        {
            ["version"] = versionNo,
            ["brand"] = brand,
            ["announcement"] = new JsonObject { ["text"] = Value(SettingKey.Announcement), ["enabled"] = Value(SettingKey.AnnouncementOn) == "true" },
            ["navigation"] = navigation,
            ["ages"] = ages,
            ["footer"] = footer,
            ["delivery"] = delivery,
            ["exchange"] = new JsonObject { ["windowDays"] = content.Exchange?.WindowDays ?? 0, ["conditions"] = conditions },
            ["pages"] = summaries,
            ["popularSearches"] = searches,
        };
    }

    private static JsonObject NavJson(NavItem item)
    {
        var children = new JsonArray();
        foreach (var child in item.Children.Where(c => c.IsEnabled).OrderBy(c => c.SortSeq))
        {
            children.Add(NavJson(child));
        }

        // The storefront's "group" is any entry that opens members: a domain Group, or a Link that has some.
        var kind = item.Kind switch
        {
            NavKind.CollectionList => "collection-list",
            NavKind.Group => "group",
            _ => children.Count > 0 ? "group" : "link",
        };

        var json = new JsonObject { ["key"] = item.Key, ["label"] = item.Label, ["kind"] = kind };
        if (item.Target is not null)
        {
            json["target"] = TargetJson(item.Target);
        }

        json["enabled"] = true;
        json["sort"] = item.SortSeq;
        json["children"] = children;
        if (item.ShowAges)
        {
            json["showAges"] = true;
        }

        return json;
    }

    private static JsonObject TargetJson(LinkTarget target) => target.Kind switch
    {
        LinkTargetKind.Page => new JsonObject { ["kind"] = "page", ["slug"] = target.Reference },
        LinkTargetKind.Collection => new JsonObject { ["kind"] = "collection", ["code"] = target.Reference },
        LinkTargetKind.Category => new JsonObject { ["kind"] = "category", ["code"] = target.Reference, ["path"] = "/" + target.Reference },
        LinkTargetKind.External => new JsonObject { ["kind"] = "external", ["url"] = target.Reference },
        _ => new JsonObject { ["kind"] = "path", ["path"] = target.Reference },
    };

    private static JsonObject PageJson(Page page)
    {
        var blocks = new JsonArray();
        foreach (var block in page.Blocks.OrderBy(b => b.SortSeq))
        {
            blocks.Add(BlockJson(block));
        }

        return new JsonObject
        {
            ["slug"] = page.Slug,
            ["title"] = page.Title,
            ["description"] = page.MetaDescription,
            ["status"] = "published",
            ["blocks"] = blocks,
        };
    }

    private static JsonObject BlockJson(PageBlock block)
    {
        var rows = block.Items.GroupBy(i => i.RowNo).OrderBy(g => g.Key)
            .Select(g => g.OrderBy(i => i.ColumnNo).Select(i => i.Text).ToList())
            .ToList();

        return block.Type switch
        {
            BlockType.Heading => new JsonObject { ["type"] = "heading", ["level"] = block.Level, ["text"] = block.Text },
            BlockType.List => new JsonObject { ["type"] = "list", ["ordered"] = block.IsOrdered, ["items"] = Strings(rows.Select(r => string.Join(" ", r))) },
            BlockType.Table => TableJson(block, rows),
            BlockType.Callout => new JsonObject { ["type"] = "callout", ["text"] = block.Text },
            BlockType.Fact => FactJson(block),
            _ => new JsonObject { ["type"] = "paragraph", ["text"] = block.Text },
        };
    }

    private static JsonObject TableJson(PageBlock block, List<List<string>> rows)
    {
        var json = new JsonObject();
        json["type"] = "table";
        if (!string.IsNullOrEmpty(block.Label))
        {
            json["caption"] = block.Label;
        }

        json["headers"] = Strings(rows.FirstOrDefault() ?? []);
        var body = new JsonArray();
        foreach (var row in rows.Skip(1))
        {
            body.Add(Strings(row));
        }

        json["rows"] = body;
        return json;
    }

    private static JsonObject FactJson(PageBlock block)
    {
        var json = new JsonObject { ["type"] = "fact", ["key"] = block.FactKey };
        if (!string.IsNullOrEmpty(block.Label))
        {
            json["label"] = block.Label;
        }

        return json;
    }

    private static JsonArray Strings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
