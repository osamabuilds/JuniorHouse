using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Tests.Support;

internal static class TestData
{
    public static NavItemDto Link(string key, string label, string path, int sort, params NavItemDto[] children) =>
        new(key, label, NavKind.Link, new TargetDto(LinkTargetKind.Path, path), sort, true, false, children);

    public static NavItemDto Group(string key, string label, int sort, params NavItemDto[] children) =>
        new(key, label, NavKind.Group, null, sort, true, false, children);

    public static PageBlockDto Paragraph(string text) => new(BlockType.Paragraph, text, 0, null, null, false, []);

    public static PageBlockDto Heading(string text, int level = 2) => new(BlockType.Heading, text, level, null, null, false, []);

    public static PageBlockDto Fact(string key, string? label = null) => new(BlockType.Fact, string.Empty, 0, key, label, false, []);
}
