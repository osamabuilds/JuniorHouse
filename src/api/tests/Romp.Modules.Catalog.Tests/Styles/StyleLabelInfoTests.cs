using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application.Styles;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Catalog.Infrastructure.Styles;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Styles;

/// <summary>Sprint 5 (labelling, D4, Q13): what the label feature may read about a style, and when the price last changed.</summary>
public sealed class StyleLabelInfoTests
{
    private static async Task<(IServiceProvider Services, long StyleId)> CreateStyleAsync(string name)
    {
        var services = TestServices.Build(name);
        var created = await services.GetRequiredService<ISender>().Send(new CreateStyleCommand(
            "STY-LBL", "Label Tee", null, 1, 1, 1, 1, 200m, 600m,
            ColourIds: [1], SizeIds: [1], TargetLines: [new StyleTargetLineDto(1, 1, 20)]));
        return (services, created.Id);
    }

    private static Task<UpdateStyleCommand> UpdateAsync(long id, decimal price) => Task.FromResult(new UpdateStyleCommand(
        id, "Label Tee", null, 1, 1, 1, 1, 200m, price,
        ColourIds: [1], SizeIds: [1], TargetLines: [new StyleTargetLineDto(1, 1, 20)]));

    [Fact]
    [Trait("Spec", "TAG-3")]
    public async Task GetLabelInfo_ReturnsCodeNameAndTargetPrice_AsDecimal()
    {
        var (services, id) = await CreateStyleAsync(Guid.NewGuid().ToString());
        var queries = new StyleQueries(services.GetRequiredService<CatalogDbContext>());

        var info = await queries.GetLabelInfoAsync([id], CancellationToken.None);

        var row = Assert.Single(info);
        Assert.Equal("STY-LBL", row.Value.Code);
        Assert.Equal("Label Tee", row.Value.Name);
        Assert.Equal(600m, row.Value.TargetRetailPrice);
        Assert.True(row.Value.IsActive);
    }

    [Fact]
    [Trait("Spec", "LW-4")]
    public async Task GetLabelInfo_IncludesRetiredStyles_WithTheActiveFlagOff()
    {
        var (services, id) = await CreateStyleAsync(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<CatalogDbContext>();
        db.Styles.Single(s => s.Id == id).Retire();
        await db.SaveChangesAsync();

        var info = await new StyleQueries(db).GetLabelInfoAsync([id], CancellationToken.None);

        Assert.False(info[id].IsActive);
    }

    [Fact]
    [Trait("Spec", "LW-5")]
    public async Task PriceChangedAt_MovesOnlyWhenThePriceChanges()
    {
        var (services, id) = await CreateStyleAsync(Guid.NewGuid().ToString());
        var sender = services.GetRequiredService<ISender>();
        var queries = new StyleQueries(services.GetRequiredService<CatalogDbContext>());
        var created = (await queries.GetLabelInfoAsync([id], CancellationToken.None))[id];

        await sender.Send(await UpdateAsync(id, 600m));
        var unchanged = (await queries.GetLabelInfoAsync([id], CancellationToken.None))[id];
        Assert.Equal(created.TargetPriceChangedAt, unchanged.TargetPriceChangedAt);

        await Task.Delay(20);
        await sender.Send(await UpdateAsync(id, 650m));
        var changed = (await queries.GetLabelInfoAsync([id], CancellationToken.None))[id];
        Assert.Equal(650m, changed.TargetRetailPrice);
        Assert.True(changed.TargetPriceChangedAt > created.TargetPriceChangedAt);
    }

    [Fact]
    public async Task GetLabelInfo_UnknownIds_AreSimplyAbsent()
    {
        var (services, _) = await CreateStyleAsync(Guid.NewGuid().ToString());
        var queries = new StyleQueries(services.GetRequiredService<CatalogDbContext>());

        var info = await queries.GetLabelInfoAsync([999_999], CancellationToken.None);

        Assert.Empty(info);
    }
}
