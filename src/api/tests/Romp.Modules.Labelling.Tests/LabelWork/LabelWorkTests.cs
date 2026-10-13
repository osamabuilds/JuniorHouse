using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Infrastructure.Persistence;
using Romp.Modules.Labelling.Tests.Support;
using Romp.Modules.Quality.Contracts;
using ValidationException = Romp.BuildingBlocks.Application.ValidationException;

namespace Romp.Modules.Labelling.Tests.LabelWork;

[Collection(LabellingPostgresFixtureDefinition.Name)]
public sealed class LabelWorkTests(LabellingPostgres postgres)
{
    private static async Task<List<Domain.LabelWork.LabelWorkItem>> ItemsAsync(LabellingPostgres postgres, long styleId)
    {
        await using var db = postgres.NewContext();
        return await db.WorkItems.AsNoTracking().Where(i => i.PoId == styleId).OrderBy(i => i.Id).ToListAsync();
    }

    [Fact]
    [Trait("Spec", "LW-1")]
    [Trait("Spec", "LW-8")]
    public async Task AcceptedBatch_BecomesOneReadyWorkItemPerLine_WithItsOutcome()
    {
        using var host = new LabellingHost(postgres);
        var (poId, _) = host.NewStyle();

        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Concession, poId, (1, 10, 12), (2, 10, 6))));

        var items = await ItemsAsync(postgres, poId);
        Assert.Equal(2, items.Count);
        Assert.All(items, i =>
        {
            Assert.Equal(LabelWorkStatus.Ready, i.StatusId);
            Assert.Equal(LabelOutcome.Concession, i.OutcomeId);
            Assert.NotNull(i.VariantId);
            Assert.False(string.IsNullOrEmpty(i.InsrBy));
        });
        Assert.Equal([12, 6], items.Select(i => i.Qty));
    }

    [Fact]
    [Trait("Spec", "LW-2")]
    public async Task TheSameMessageTwice_ChangesNothing_AndTakesNoGtinNumber()
    {
        using var host = new LabellingHost(postgres);
        var (poId, _) = host.NewStyle();
        var batch = Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 12));
        await host.SendAsync(new RecordAcceptedBatchCommand(batch));
        var after1 = await host.InScopeAsync(sp => sp.GetRequiredService<Application.Abstractions.IGtinSequence>().CurrentAsync("0200000", default));

        await host.SendAsync(new RecordAcceptedBatchCommand(batch));

        Assert.Single(await ItemsAsync(postgres, poId));
        var after2 = await host.InScopeAsync(sp => sp.GetRequiredService<Application.Abstractions.IGtinSequence>().CurrentAsync("0200000", default));
        Assert.Equal(after1, after2);
    }

    [Fact]
    [Trait("Spec", "LW-4")]
    public async Task APoWithNoStyle_IsParkedWithAReason_TakesNoGtin_AndCanBeResolvedLater()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        host.Lookup.StyleByPo[poId] = null; // the PO's style cannot be found
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5))));

        var parked = Assert.Single(await ItemsAsync(postgres, poId));
        Assert.Equal(LabelWorkStatus.NeedsAttention, parked.StatusId);
        Assert.Equal(ParkReasons.NoStyle, parked.ParkReasonId);
        Assert.Null(parked.VariantId);
        await using (var db = postgres.NewContext())
        {
            Assert.False(await db.Variants.AnyAsync(v => v.StyleId == styleId));
        }

        host.Lookup.StyleByPo[poId] = styleId; // the cause is fixed
        var resolved = await host.SendAsync(new ResolveWorkItemCommand(parked.Id, false, "Asha"));

        Assert.Equal("Ready", resolved.StatusCode);
        Assert.NotNull(resolved.Gtin);
        Assert.Null(resolved.ParkReasonCode);
    }

    [Fact]
    [Trait("Spec", "LW-4")]
    public async Task ARetiredStyle_IsParkedUntilStaffConfirm_ThenTheGoodsAreLabelled()
    {
        using var host = new LabellingHost(postgres);
        var (poId, _) = host.NewStyle(active: false);
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5))));
        var item = Assert.Single(await ItemsAsync(postgres, poId));
        Assert.Equal(ParkReasons.StyleRetired, item.ParkReasonId);

        var stillParked = await host.SendAsync(new ResolveWorkItemCommand(item.Id, ConfirmRetiredStyle: false, "Asha"));
        Assert.Equal("NeedsAttention", stillParked.StatusCode);
        Assert.False(stillParked.StyleIsActive);

        var confirmed = await host.SendAsync(new ResolveWorkItemCommand(item.Id, ConfirmRetiredStyle: true, "Asha"));
        Assert.Equal("Ready", confirmed.StatusCode);
        Assert.NotNull(confirmed.Gtin);
    }

    [Fact]
    [Trait("Spec", "LW-4")]
    public async Task AnUnknownColourCode_ParksOnlyThatLine_AndTheRestProceeds()
    {
        using var host = new LabellingHost(postgres);
        var (poId, _) = host.NewStyle();

        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5), (1, 99, 5))));

        var items = await ItemsAsync(postgres, poId);
        Assert.Equal(LabelWorkStatus.Ready, items.Single(i => i.ColourId == 10).StatusId);
        var bad = items.Single(i => i.ColourId == 99);
        Assert.Equal(ParkReasons.UnknownCode, bad.ParkReasonId);
    }

    [Fact]
    [Trait("Spec", "LW-4")]
    public async Task Resolve_OnAnUnknownItem_IsNotFound_AndNeedsAPerson()
    {
        using var host = new LabellingHost(postgres);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new ResolveWorkItemCommand(long.MaxValue, false, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ResolveWorkItemCommand(1, false, "")));
    }

    [Fact]
    [Trait("Spec", "LW-7")]
    public async Task TheWorklist_IsPaged_Filtered_AndRunsAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new LabellingHost(postgres, counter: counter);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5), (2, 10, 5), (3, 10, 5), (1, 11, 5), (2, 11, 5))));

        counter.Reset();
        var page1 = await host.SendAsync(new SearchLabelWorkQuery(Status: "ready", StyleId: styleId, Page: 1, PageSize: 3));
        var queries = counter.Count;
        var page2 = await host.SendAsync(new SearchLabelWorkQuery(Status: "ready", StyleId: styleId, Page: 2, PageSize: 3));

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
        Assert.True(page1.Items.Zip(page1.Items.Skip(1)).All(p => p.First.Id > p.Second.Id));
        Assert.True(queries <= 6, $"a page of the worklist must not run per row: {queries} queries");
        Assert.All(page1.Items, i =>
        {
            Assert.Equal($"Style {styleId}", i.StyleName);
            Assert.NotNull(i.SkuCode);
            Assert.Equal(i.Qty, i.RemainingQty);
        });
        Assert.Empty((await host.SendAsync(new SearchLabelWorkQuery(Status: "attention", StyleId: styleId))).Items);
    }

    [Fact]
    [Trait("Spec", "PJ-1")]
    public async Task QueueingTags_NeverExceedsTheAcceptedQuantity_ButTheSpareAllowanceAddsRoom()
    {
        using var host = new LabellingHost(postgres, o => o.SparePercent = 10m);
        var (poId, _) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 10))));
        var item = Assert.Single(await ItemsAsync(postgres, poId));

        var first = await host.SendAsync(new QueueTagsCommand(item.Id, 6, null, "Asha"));
        Assert.Equal("Queued", first.StatusCode);
        Assert.Equal(6, first.Qty);

        // 10 accepted + 10% spare = 11 allowed; 6 are already queued, so 5 more is the most.
        var tooMany = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new QueueTagsCommand(item.Id, 6, null, "Asha")));
        Assert.Contains("5", tooMany.Errors["Qty"][0], StringComparison.Ordinal);
        await host.SendAsync(new QueueTagsCommand(item.Id, 5, null, "Asha"));

        var none = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new QueueTagsCommand(item.Id, 1, null, "Asha")));
        Assert.Contains("already queued", none.Errors["Qty"][0], StringComparison.Ordinal);

        var row = (await host.SendAsync(new SearchLabelWorkQuery(StyleId: item.StyleId))).Items.Single();
        Assert.Equal(11, row.ActiveQty);
        Assert.Equal(0, row.RemainingQty);
    }

    [Fact]
    [Trait("Spec", "PJ-1")]
    public async Task ACancelledJob_GivesItsTagsBack()
    {
        using var host = new LabellingHost(postgres);
        var (poId, _) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 10))));
        var item = Assert.Single(await ItemsAsync(postgres, poId));
        var job = await host.SendAsync(new QueueTagsCommand(item.Id, 10, null, "Asha"));

        await host.SendAsync(new CancelPrintJobCommand(job.Id, "Asha"));

        var again = await host.SendAsync(new QueueTagsCommand(item.Id, 10, null, "Asha"));
        Assert.Equal(10, again.Qty);
    }

    [Fact]
    [Trait("Spec", "PJ-1")]
    public async Task TwoClicksAtOnce_CannotBothPass_TheRemainingQuantityCheck()
    {
        using var host = new LabellingHost(postgres);
        var (poId, _) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 10))));
        var item = Assert.Single(await ItemsAsync(postgres, poId));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                return (Job: (PrintJobDto?)await host.SendAsync(new QueueTagsCommand(item.Id, 6, null, "Asha")), Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Job: (PrintJobDto?)null, Error: (Exception?)ex);
            }
        }));

        Assert.Equal(1, results.Count(r => r.Job is not null));
        Assert.All(results.Where(r => r.Error is not null), r => Assert.IsType<ValidationException>(r.Error));
        await using var db = postgres.NewContext();
        Assert.Equal(6, await db.PrintJobs.Where(j => j.WorkItemId == item.Id).SumAsync(j => j.Qty));
    }

    [Fact]
    [Trait("Spec", "PJ-1")]
    public async Task QueueingTags_NeedsAReadyItem_AndSaneInput()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        host.Lookup.StyleByPo[poId] = null;
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 10))));
        var parked = Assert.Single(await ItemsAsync(postgres, poId));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new QueueTagsCommand(parked.Id, 1, null, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new QueueTagsCommand(parked.Id, 0, null, "Asha")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new QueueTagsCommand(parked.Id, 5_001, null, "Asha")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new QueueTagsCommand(long.MaxValue, 1, null, "Asha")));
        Assert.Equal(styleId, styleId);
    }

    [Fact]
    [Trait("Spec", "LW-5")]
    public async Task PriceChangedAfterTheTagsWereQueued_IsFlagged_UntilAReprintPrintsTheCurrentPrice()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle(price: 1000m);
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 10))));
        var item = Assert.Single(await ItemsAsync(postgres, poId));

        host.Time.Now = LabellingHost.Start;
        await host.SendAsync(new QueueTagsCommand(item.Id, 10, null, "Asha"));
        Assert.False((await host.SendAsync(new GetLabelWorkItemQuery(item.Id))).PriceChanged);

        // The target price changes after the tags were queued.
        host.Lookup.Styles[styleId] = host.Lookup.Styles[styleId] with { TargetRetailPrice = 1100m, TargetPriceChangedAt = LabellingHost.Start.AddHours(1) };
        Assert.True((await host.SendAsync(new GetLabelWorkItemQuery(item.Id))).PriceChanged);
        var flagged = await host.SendAsync(new SearchLabelWorkQuery(StyleId: styleId, PriceFlag: true));
        Assert.Equal(item.Id, Assert.Single(flagged.Items).Id);

        // A reprint puts the current price on the tag, which clears the flag. Nothing was blocked along the way.
        host.Time.Now = LabellingHost.Start.AddHours(2);
        var variantId = item.VariantId!.Value;
        var reprint = await host.SendAsync(new ReprintTagCommand(variantId, 10, ReprintReasons.PriceChange, null, null, "Asha"));
        Assert.Equal(1100m, reprint.PriceSnapshot);
        Assert.False((await host.SendAsync(new GetLabelWorkItemQuery(item.Id))).PriceChanged);
        Assert.Empty((await host.SendAsync(new SearchLabelWorkQuery(StyleId: styleId, PriceFlag: true))).Items);
    }
}

public sealed class QcBatchAcceptedMapperTests
{
    private static QcBatchAcceptedEvent Message(string outcome = "Accepted", params QcLine[] lines) =>
        new(BatchId: 2, SubBatchId: 3, GrnId: 4, PoId: 5, PoNo: "PO-2026-00005", outcome, lines.Length == 0 ? [new QcLine(1, 10, 5)] : lines);

    [Fact]
    [Trait("Spec", "LW-1")]
    public void AValidMessage_IsMapped_WithItsOutcomeAndLines()
    {
        var result = QcBatchAcceptedMapper.Map(Message("Concession", new QcLine(1, 10, 5), new QcLine(2, 10, 7)), 500);

        Assert.True(result.IsValid);
        Assert.Equal(LabelOutcome.Concession, result.Batch!.OutcomeId);
        Assert.Equal(3, result.Batch.SubBatchId);
        Assert.Equal(2, result.Batch.Lines.Count);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Rework")]
    [InlineData("")]
    [InlineData("accepted")]
    [Trait("Spec", "LW-3")]
    public void AnythingButAcceptedOrConcession_IsRefused(string outcome) =>
        Assert.False(QcBatchAcceptedMapper.Map(Message(outcome), 500).IsValid);

    [Fact]
    [Trait("Spec", "LW-6")]
    public void MalformedMessages_AreRefused_BeforeAnythingIsStored()
    {
        Assert.False(QcBatchAcceptedMapper.Map(Message("Accepted", new QcLine(1, 10, 0)), 500).IsValid, "zero quantity");
        Assert.False(QcBatchAcceptedMapper.Map(Message("Accepted", new QcLine(1, 10, -4)), 500).IsValid, "negative quantity");
        Assert.False(QcBatchAcceptedMapper.Map(Message("Accepted", new QcLine(0, 10, 4)), 500).IsValid, "no size");
        Assert.False(QcBatchAcceptedMapper.Map(Message("Accepted", new QcLine(1, 0, 4)), 500).IsValid, "no colour");
        Assert.False(QcBatchAcceptedMapper.Map(Message("Accepted", new QcLine(1, 10, 4), new QcLine(1, 10, 6)), 500).IsValid, "duplicate line");
        Assert.False(QcBatchAcceptedMapper.Map(Message() with { Lines = [] }, 500).IsValid, "no lines");
        Assert.False(QcBatchAcceptedMapper.Map(Message() with { SubBatchId = 0 }, 500).IsValid, "no sub-batch id");
        Assert.False(QcBatchAcceptedMapper.Map(Message() with { PoId = -1 }, 500).IsValid, "bad PO id");
        var tooMany = Enumerable.Range(1, 501).Select(i => new QcLine((short)i, 10, 1)).ToArray();
        Assert.False(QcBatchAcceptedMapper.Map(Message("Accepted", tooMany), 500).IsValid, "more lines than the limit");
    }
}
