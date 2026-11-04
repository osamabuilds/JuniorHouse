using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Reference.Domain.Lookups.Vendors;
using Romp.Modules.Reference.Infrastructure.Lookups;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Romp.Modules.Reference.Tests.Support;

namespace Romp.Modules.Reference.Tests.Lookups;

/// <summary>customers/addresses A1 (ADD-4, ADD-7): provinces and their cities, read by other modules only through <c>IGeographyQueries</c>.</summary>
public sealed class GeographyQueriesTests
{
    private static async Task<(ReferenceDbContext Db, GeographyQueries Queries, ProvinceLookup North, ProvinceLookup South)> ArrangeAsync()
    {
        var db = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ReferenceDbContext>();
        var north = new ProvinceLookup("T-NRTH", "Northland", null, 2);
        var south = new ProvinceLookup("T-STH", "Southland", null, 1);
        var retired = new ProvinceLookup("T-OLD", "Oldland", null, 3);
        retired.Retire();
        db.Set<ProvinceLookup>().AddRange(north, south, retired);
        await db.SaveChangesAsync();

        AddCity(db, "T-AAA", "Alphaville", north.Id, 1);
        AddCity(db, "T-BBB", "Betatown", north.Id, 2);
        AddCity(db, "T-CCC", "Gammaport", south.Id, 1);
        AddCity(db, "T-DDD", "Nowhere", null, 1);
        var closed = AddCity(db, "T-EEE", "Closedtown", north.Id, 3);
        closed.Retire();
        await db.SaveChangesAsync();

        return (db, new GeographyQueries(db), north, south);
    }

    private static CityLookup AddCity(ReferenceDbContext db, string code, string name, short? provinceId, short sort)
    {
        var city = new CityLookup(code, name, null, sort);
        if (provinceId is { } id)
        {
            city.AssignProvince(id);
        }

        db.Set<CityLookup>().Add(city);
        return city;
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task GetProvinces_ReturnsActiveProvincesInSortOrder()
    {
        var (_, queries, _, _) = await ArrangeAsync();

        var provinces = (await queries.GetProvincesAsync(CancellationToken.None)).Where(p => p.Code.StartsWith("T-", StringComparison.Ordinal)).ToList();

        Assert.Equal(["Southland", "Northland"], provinces.Select(p => p.Name));
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task GetCities_ReturnsOnlyActiveCitiesOfThatProvince_InSortOrder()
    {
        var (_, queries, north, _) = await ArrangeAsync();

        var cities = await queries.GetCitiesAsync(north.Id, CancellationToken.None);
        Assert.NotNull(cities);

        Assert.Equal(["Alphaville", "Betatown"], cities.Select(c => c.Name));
        Assert.All(cities, c => Assert.Equal(north.Id, c.ProvinceId));
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task GetCities_ForAnUnknownOrRetiredProvince_IsNull()
    {
        var (db, queries, _, _) = await ArrangeAsync();
        var retiredId = db.Set<ProvinceLookup>().Single(p => p.Code == "T-OLD").Id;

        Assert.Null(await queries.GetCitiesAsync(31_000, CancellationToken.None));
        Assert.Null(await queries.GetCitiesAsync(retiredId, CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task CityBelongsToProvince_AcceptsOnlyAnActiveCityInItsOwnActiveProvince()
    {
        var (db, queries, north, south) = await ArrangeAsync();
        short Id(string code) => db.Set<CityLookup>().Single(c => c.Code == code).Id;

        Assert.True(await queries.CityBelongsToProvinceAsync(Id("T-AAA"), north.Id, CancellationToken.None));
        Assert.False(await queries.CityBelongsToProvinceAsync(Id("T-AAA"), south.Id, CancellationToken.None));
        Assert.False(await queries.CityBelongsToProvinceAsync(Id("T-DDD"), north.Id, CancellationToken.None));
        Assert.False(await queries.CityBelongsToProvinceAsync(Id("T-EEE"), north.Id, CancellationToken.None));
        Assert.False(await queries.CityBelongsToProvinceAsync(31_000, north.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "ADD-7")]
    public async Task GetByIds_NamesRowsInOneCallEach_IncludingRetiredOnes_AndSkipsUnknown()
    {
        var (db, queries, north, _) = await ArrangeAsync();
        var closedId = db.Set<CityLookup>().Single(c => c.Code == "T-EEE").Id;
        var aaaId = db.Set<CityLookup>().Single(c => c.Code == "T-AAA").Id;

        var cities = await queries.GetCitiesByIdAsync([aaaId, closedId, 31_000], CancellationToken.None);
        var provinces = await queries.GetProvincesByIdAsync([north.Id, 31_000], CancellationToken.None);

        Assert.Equal(2, cities.Count);
        Assert.Equal("Closedtown", cities[closedId].Name);
        Assert.Equal(north.Id, cities[aaaId].ProvinceId);
        Assert.Equal("Northland", provinces[north.Id].Name);
        Assert.Single(provinces);
        Assert.Empty(await queries.GetCitiesByIdAsync([], CancellationToken.None));
    }
}
