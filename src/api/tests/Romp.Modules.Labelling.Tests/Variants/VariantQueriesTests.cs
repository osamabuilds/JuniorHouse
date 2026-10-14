using Microsoft.EntityFrameworkCore;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Infrastructure.Variants;
using Romp.Modules.Labelling.Tests.Support;

namespace Romp.Modules.Labelling.Tests.Variants;

/// <summary>Sprint 7 (Catalog plan G5, ADR 0014): Catalog reads a style's variants through the Labelling contract.</summary>
[Collection(LabellingPostgresFixtureDefinition.Name)]
public sealed class VariantQueriesTests(LabellingPostgres postgres)
{
    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task ReturnsEveryVariantOfTheStyle_InOneQuery()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5), (2, 11, 7), (2, 10, 3))));
        var counter = new CommandCounter();
        await using var db = postgres.NewContext(counter);

        var variants = await new VariantContractQueries(db).GetByStyleAsync(styleId, default);

        Assert.Equal(3, variants.Count);
        Assert.All(variants, v => Assert.False(v.GtinVoided));
        Assert.All(variants, v => Assert.Equal(13, v.Gtin.Length));
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    [Trait("Spec", "PRD-14")]
    public async Task AVoidedVariant_ReportsItsLastGtinAsVoided_AndAnUnknownStyleIsEmpty()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5))));
        await using var db = postgres.NewContext();
        var variantId = await db.Variants.Where(v => v.StyleId == styleId).Select(v => v.Id).SingleAsync();
        await host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Error, null, "Test Person"));
        var queries = new VariantContractQueries(db);

        var voided = await queries.GetByIdAsync(variantId, default);

        Assert.NotNull(voided);
        Assert.True(voided.GtinVoided);
        Assert.Empty(await queries.GetByStyleAsync(long.MaxValue, default));
        Assert.Null(await queries.GetByIdAsync(long.MaxValue, default));
    }
}
