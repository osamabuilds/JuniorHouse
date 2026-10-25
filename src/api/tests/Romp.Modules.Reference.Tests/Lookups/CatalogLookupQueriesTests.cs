using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Infrastructure.Lookups;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Romp.Modules.Reference.Tests.Support;

namespace Romp.Modules.Reference.Tests.Lookups;

/// <summary>Sprint 7 (plan G2): Catalog reads gender, category, collection, fibre and care lookups through the contract.</summary>
public sealed class CatalogLookupQueriesTests
{
    private static ReferenceDbContext NewDb() => TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ReferenceDbContext>();

    [Fact]
    [Trait("Spec", "FR-53")]
    public async Task Queries_ReturnCodesForKnownIdsOnly_AndTheCareSymbol()
    {
        var db = NewDb();
        db.Set<GenderLookup>().Add(new GenderLookup("GIRLS", "Girls", null, 1));
        db.Set<CareInstructionLookup>().Add(new CareInstructionLookup("WASH-30", "Machine wash at 30 C", null, 1, "wash-30"));
        db.Set<FibreLookup>().Add(new FibreLookup("COTTON", "Cotton", null, 1));
        await db.SaveChangesAsync();
        var genderId = db.Set<GenderLookup>().Single().Id;
        var careId = db.Set<CareInstructionLookup>().Single().Id;
        var fibreId = db.Set<FibreLookup>().Single().Id;
        var queries = new CatalogLookupQueries(db);

        var genders = await queries.GetGendersAsync([genderId, 31_000], CancellationToken.None);
        var care = await queries.GetCareInstructionsAsync([careId, 31_000], CancellationToken.None);
        var fibres = await queries.GetFibresAsync([fibreId], CancellationToken.None);

        Assert.Equal("GIRLS", genders[genderId].Code);
        Assert.False(genders.ContainsKey(31_000));
        Assert.Equal("wash-30", care[careId].SymbolCode);
        Assert.False(care.ContainsKey(31_000));
        Assert.Equal("Cotton", fibres[fibreId].Name);
    }

    [Fact]
    [Trait("Spec", "NAV-5")]
    [Trait("Spec", "NAV-9")]
    public async Task EachList_ReadsItsOwnLookup_ActiveRowsFirstThenBySortOrder_WithRetiredRowsFlagged()
    {
        var db = NewDb();
        var retired = new ColourLookup("OLD", "Old shade", null, 0);
        retired.Retire();
        db.Set<ColourLookup>().AddRange(new ColourLookup("RED", "Red", null, 2), new ColourLookup("BLUE", "Blue", null, 1), retired);
        db.Set<GenderLookup>().Add(new GenderLookup("GIRLS", "Girls", null, 1));
        db.Set<CategoryLookup>().Add(new CategoryLookup("DRESSES", "Dresses", null, 1, null));
        db.Set<AgeBracketLookup>().Add(new AgeBracketLookup("KIDS", "Kids 4-8Y", null, 1));
        db.Set<CollectionLookup>().Add(new CollectionLookup("SALE", "Sale", null, 1));
        db.Set<SizeLookup>().AddRange(new SizeLookup("5-6Y", "5-6 Years", null, 2), new SizeLookup("3-4Y", "3-4 Years", null, 1));
        await db.SaveChangesAsync();
        var queries = new CatalogLookupQueries(db);

        var colours = await queries.ListColoursAsync(CancellationToken.None);

        Assert.Equal(["BLUE", "RED", "OLD"], colours.Select(c => c.Code));
        Assert.Equal([true, true, false], colours.Select(c => c.IsActive));
        Assert.Equal((short)1, colours[0].SortSeq);
        Assert.Equal(["GIRLS"], (await queries.ListGendersAsync(CancellationToken.None)).Select(c => c.Code));
        Assert.Equal(["DRESSES"], (await queries.ListCategoriesAsync(CancellationToken.None)).Select(c => c.Code));
        Assert.Equal(["KIDS"], (await queries.ListAgeBracketsAsync(CancellationToken.None)).Select(c => c.Code));
        Assert.Equal(["SALE"], (await queries.ListCollectionsAsync(CancellationToken.None)).Select(c => c.Code));
        Assert.Equal(["3-4Y", "5-6Y"], (await queries.ListSizesAsync(CancellationToken.None)).Select(c => c.Code));
    }

    [Fact]
    public async Task Get_EmptyIdList_ReturnsEmpty()
    {
        var queries = new CatalogLookupQueries(NewDb());

        Assert.Empty(await queries.GetCollectionsAsync([], CancellationToken.None));
        Assert.Empty(await queries.GetCategoriesAsync([], CancellationToken.None));
        Assert.Empty(await queries.GetCareInstructionsAsync([], CancellationToken.None));
    }
}
