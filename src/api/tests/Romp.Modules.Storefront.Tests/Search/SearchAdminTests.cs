using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 10: the staff handlers for synonym groups and the zero-result log (SRC-12, 13).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchAdminTests(StorefrontPostgres postgres)
{
    private static readonly SeedVariant InStock = new("RED", "5-6Y", 800m, 10);

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task List_ShowsTheSeededGroupsWithTheirTerms_InTwoQueriesAPage()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var page = await host.SendAsync(new ListSynonymGroupsQuery(1, 3));

        Assert.Equal(3, page.Items.Count);
        Assert.True(page.TotalCount >= 7);
        Assert.All(page.Items, g => Assert.True(g.Terms.Count >= 2));
        Assert.Contains(page.Items.Concat((await host.SendAsync(new ListSynonymGroupsQuery(2, 3))).Items), g => g.Terms.Contains("frock"));
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Create_AddsAGroup_AndSearchFindsItAtOnceWithoutARestart()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [InStock], name: "Cotton lehenga");
        Assert.Empty((await host.SendAsync(new Application.Search.SearchProductsQuery("ghagra", Filter()))).Page.Tiles);

        var group = await host.SendAsync(new CreateSynonymGroupCommand("lehenga", true, ["Ghagra", " lehenga ", "lehnga"]));

        Assert.Equal(["ghagra", "lehenga", "lehnga"], group.Terms);
        var found = await host.SendAsync(new Application.Search.SearchProductsQuery("ghagra", Filter()));
        Assert.Equal(["Cotton lehenga"], found.Page.Tiles.Select(t => t.Name));
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Create_ATermAlreadyInAnotherGroup_IsRefused_AndNothingIsStored()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var before = (await host.SendAsync(new ListSynonymGroupsQuery(1, 50))).TotalCount;

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new CreateSynonymGroupCommand("my frocks", true, ["frock", "pinafore"])));

        Assert.Contains("\"frock\" already belongs to another group.", error.Errors["terms"]);
        Assert.Equal(before, (await host.SendAsync(new ListSynonymGroupsQuery(1, 50))).TotalCount);
    }

    [Theory]
    [Trait("Spec", "SRC-12")]
    [InlineData("", new[] { "a1", "b1" })]
    [InlineData("ok", new[] { "only-one" })]
    [InlineData("ok", new[] { "x1", "x1" })]
    [InlineData("ok", new[] { "bad<term>", "fine" })]
    [InlineData("ok", new[] { "one two three four", "fine" })]
    [InlineData("ok", new[] { "", "fine" })]
    public async Task Create_InvalidInput_IsRefusedWithFieldMessages(string name, string[] terms)
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new CreateSynonymGroupCommand(name, true, terms)));

        Assert.NotEmpty(error.Errors);
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Update_ChangesNameActiveAndTerms_AndAStaleVersionIsRefused()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var created = await host.SendAsync(new CreateSynonymGroupCommand("bags", true, ["bag", "tote"]));

        var updated = await host.SendAsync(new UpdateSynonymGroupCommand(created.Id, created.RowVersion, "bags and totes", true, ["bag", "satchel"]));

        Assert.Equal(("bags and totes", true), (updated.Name, updated.IsActive));
        Assert.Equal(["bag", "satchel"], updated.Terms);
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            host.SendAsync(new UpdateSynonymGroupCommand(created.Id, created.RowVersion, "stale", true, ["bag", "satchel"])));
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Update_AnInactiveGroup_StopsExpandingSearches()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [InStock], name: "Cotton kameez");
        var kurta = (await host.SendAsync(new ListSynonymGroupsQuery(1, 50))).Items.Single(g => g.Terms.Contains("kurta"));
        Assert.Single((await host.SendAsync(new Application.Search.SearchProductsQuery("kurta", Filter()))).Page.Tiles);

        await host.SendAsync(new UpdateSynonymGroupCommand(kurta.Id, kurta.RowVersion, kurta.Name, false, kurta.Terms));

        Assert.Empty((await host.SendAsync(new Application.Search.SearchProductsQuery("kurta", Filter()))).Page.Tiles);
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Update_UnknownGroup_IsNotFound()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new UpdateSynonymGroupCommand(9999, 0, "x", true, ["aa", "bb"])));
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Delete_RemovesTheGroupAndItsTerms()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var created = await host.SendAsync(new CreateSynonymGroupCommand("bags", true, ["bag", "tote"]));

        Assert.True(await host.SendAsync(new DeleteSynonymGroupCommand(created.Id)));
        Assert.False(await host.SendAsync(new DeleteSynonymGroupCommand(created.Id)));
        Assert.DoesNotContain(await host.QueryAsync(db => db.SynonymTerms.Select(t => t.Term).ToListAsync()), t => t is "bag" or "tote");
    }

    [Fact]
    [Trait("Spec", "SRC-13")]
    public async Task Misses_AreListedMostFrequentFirst_AndPaged()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        foreach (var (text, times) in new[] { ("lehenga", 3), ("ghagra", 5), ("tutu", 1) })
        {
            for (var i = 0; i < times; i++)
            {
                await host.SendAsync(new RecordMissCommand(text));
            }
        }

        var first = await host.SendAsync(new ListMissesQuery(1, 2));
        var second = await host.SendAsync(new ListMissesQuery(2, 2));

        Assert.Equal(3, first.TotalCount);
        Assert.Equal([("ghagra", 5), ("lehenga", 3)], first.Items.Select(m => (m.Text, m.Count)));
        Assert.Equal([("tutu", 1)], second.Items.Select(m => (m.Text, m.Count)));
    }

    private static Romp.Modules.Storefront.Domain.Listings.ListingFilter Filter() =>
        Romp.Modules.Storefront.Domain.Listings.ListingFilter.Parse(null, null, null, null, null, null, null).Filter!;
}
