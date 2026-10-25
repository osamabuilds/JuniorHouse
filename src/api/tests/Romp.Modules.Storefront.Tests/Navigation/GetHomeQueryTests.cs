using Romp.Modules.Storefront.Application.Navigation;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Navigation;

/// <summary>Navigation task 4: <see cref="GetHomeQuery"/> (NAV-10).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class GetHomeQueryTests(StorefrontPostgres postgres)
{
    [Fact]
    [Trait("Spec", "NAV-10")]
    public async Task Home_ShowsNewArrivalsNewestFirst_AndAnAudienceTileForEachAudience()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-3), name: "Older");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS", publishedAt: DateTimeOffset.UtcNow.AddDays(-1), name: "Newer");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-80), name: "Outside the window");

        var home = await host.SendAsync(new GetHomeQuery());

        Assert.False(home.IsFallback);
        Assert.Equal(["Newer", "Older"], home.NewArrivals.Select(t => t.Name).ToArray());
        Assert.Equal([("Girls", "/girls"), ("Boys", "/boys")], home.Audiences.Select(a => (a.Name, a.Path)).ToArray());
    }

    [Fact]
    [Trait("Spec", "NAV-10")]
    public async Task WhenNothingIsNew_HomeFallsBackToTheNewestListableProducts()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-90), name: "Old one");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-60), name: "Old two");

        var home = await host.SendAsync(new GetHomeQuery());

        Assert.True(home.IsFallback);
        Assert.Equal(["Old two", "Old one"], home.NewArrivals.Select(t => t.Name).ToArray());
    }

    [Fact]
    [Trait("Spec", "NAV-10")]
    public async Task WithNothingListable_HomeHasNoHero_ButStillAnswers()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);

        var home = await host.SendAsync(new GetHomeQuery());

        Assert.Empty(home.NewArrivals);
        Assert.Empty(home.Audiences);
    }

    [Fact]
    [Trait("Spec", "NAV-10")]
    public async Task Home_ShowsAtMostTwelveTiles()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        foreach (var id in Enumerable.Range(1, 14))
        {
            await ListingSeeder.ListableAsync(host, id, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddHours(-id));
        }

        Assert.Equal(12, (await host.SendAsync(new GetHomeQuery())).NewArrivals.Count);
    }
}
