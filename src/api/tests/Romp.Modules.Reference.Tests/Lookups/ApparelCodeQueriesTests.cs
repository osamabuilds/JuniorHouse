using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Infrastructure.Lookups;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Romp.Modules.Reference.Tests.Support;

namespace Romp.Modules.Reference.Tests.Lookups;

/// <summary>Sprint 5 (D3): the SKU is built from the colour and size lookup codes, read through the contract.</summary>
public sealed class ApparelCodeQueriesTests
{
    [Fact]
    [Trait("Spec", "VAR-7")]
    public async Task GetColoursAndSizes_ReturnCodeAndName_ForKnownIdsOnly()
    {
        var services = TestServices.Build(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ReferenceDbContext>();
        db.Set<ColourLookup>().Add(new ColourLookup("RED", "Red", null, 1));
        db.Set<SizeLookup>().Add(new SizeLookup("0203", "2-3Y", null, 1));
        await db.SaveChangesAsync();
        var colourId = db.Set<ColourLookup>().Single(c => c.Code == "RED").Id;
        var sizeId = db.Set<SizeLookup>().Single(s => s.Code == "0203").Id;
        var queries = new ApparelCodeQueries(db);

        var colours = await queries.GetColoursAsync([colourId, 31_000], CancellationToken.None);
        var sizes = await queries.GetSizesAsync([sizeId], CancellationToken.None);

        Assert.Equal("RED", colours[colourId].Code);
        Assert.Equal("Red", colours[colourId].Name);
        Assert.False(colours.ContainsKey(31_000));
        Assert.Equal("0203", sizes[sizeId].Code);
        Assert.Equal("2-3Y", sizes[sizeId].Name);
    }

    [Fact]
    public async Task Get_EmptyIdList_ReturnsEmptyWithoutQuerying()
    {
        var db = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ReferenceDbContext>();

        Assert.Empty(await new ApparelCodeQueries(db).GetColoursAsync([], CancellationToken.None));
        Assert.Empty(await new ApparelCodeQueries(db).GetSizesAsync([], CancellationToken.None));
    }
}
