using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Returns.Application.ReverseQc;
using Romp.Modules.Returns.Contracts;
using Romp.Modules.Returns.Domain;
using Romp.Modules.Returns.Infrastructure.ReverseQc;
using Romp.Modules.Returns.Tests.Support;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Returns.Tests.ReverseQc;

[Collection(ReturnsPostgresFixtureDefinition.Name)]
public sealed class ReverseQcFlowTests(ReturnsPostgres postgres)
{
    private const string Bin = "A-01-01";
    private const string Staff = ReturnsHost.Staff;

    private static ReceiveReturnCaseCommand Receive(long id, bool intact, string bin = Bin) => new(id, intact, bin, Staff);

    private static DecideReturnLineCommand Decide(long caseId, long lineId, string decision, int qty, string? note = null) => new(caseId, lineId, decision, qty, note, Staff);

    private async Task<List<string>> OutboxPayloadsAsync(long orderId)
    {
        await using var db = postgres.NewContext();
        var type = nameof(ReturnQcCompletedEvent);
        return await db.Set<OutboxMessage>().Where(m => m.EventType == type && m.AggregateId == orderId).Select(m => m.Payload).ToListAsync();
    }

    // Opening (R3)

    [Fact]
    [Trait("Spec", "RQC-1")]
    public async Task AReturnedParcel_OpensOneCaseWithTheOrderLines_AndTheSameEventAgainOpensNothingMore()
    {
        using var host = new ReturnsHost(postgres);
        var (orderId, number, variants) = host.PlaceOrder(2, 1);

        var first = await host.SendAsync(new OpenReturnCaseCommand(orderId, number));
        var second = await host.SendAsync(new OpenReturnCaseCommand(orderId, number));

        Assert.NotNull(first);
        Assert.Null(second);
        var detail = await host.DetailAsync(first!.Value);
        Assert.Equal(("AWAITING_RECEIPT", number), (detail.Status, detail.OrderNumber));
        Assert.Equal(variants, detail.Lines.Select(l => l.VariantId));
        Assert.Equal([2, 1], detail.Lines.Select(l => l.OrderedQuantity));
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Cases.CountAsync(c => c.OrderId == orderId));
    }

    [Fact]
    [Trait("Spec", "RQC-1")]
    public async Task AnOrderThatDoesNotExist_OpensNothing()
    {
        using var host = new ReturnsHost(postgres);

        Assert.Null(await host.SendAsync(new OpenReturnCaseCommand(999_999_999, "RMP-NONE")));
    }

    [Fact]
    [Trait("Spec", "RQC-1")]
    public async Task TheConsumer_OpensACaseOnlyForReturnedToOrigin_AndOnlyOncePerMessage()
    {
        using var host = new ReturnsHost(postgres);
        var (orderId, number, _) = host.PlaceOrder(1);
        var (otherId, otherNumber, _) = host.PlaceOrder(1);

        async Task DeliverAsync(ShipmentStatusChangedEvent e, long messageId)
        {
            await using var scope = host.Scope();
            var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
            context.MessageId = messageId;
            context.Schema = "RTRN";
            await scope.ServiceProvider.GetRequiredService<ShipmentReturnedConsumer>().HandleAsync(e, default);
        }

        await DeliverAsync(new ShipmentStatusChangedEvent(otherId, otherNumber, ShipmentStatusCodes.Delivered, "WB1"), 9_001);
        await DeliverAsync(new ShipmentStatusChangedEvent(orderId, number, ShipmentStatusCodes.ReturnedToOrigin, "WB2"), 9_002);
        await DeliverAsync(new ShipmentStatusChangedEvent(orderId, number, ShipmentStatusCodes.ReturnedToOrigin, "WB2"), 9_002);

        await using var db = postgres.NewContext();
        Assert.Equal(0, await db.Cases.CountAsync(c => c.OrderId == otherId));
        Assert.Equal(1, await db.Cases.CountAsync(c => c.OrderId == orderId));
    }

    [Fact]
    [Trait("Spec", "RQC-13")]
    public async Task TheCase_HoldsNoNameAddressOrPhone()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(1);

        var json = JsonSerializer.Serialize(await host.DetailAsync(id));

        Assert.DoesNotContain("Sara", json);
        Assert.DoesNotContain("Lahore", json);
        await using var db = postgres.NewContext();
        var columns = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'RTRN' AND table_name IN ('RTRN_MAIN', 'RTRN_LINE')").ToListAsync();
        Assert.DoesNotContain(columns, c => c.Contains("PHON") || c.Contains("ADDR") || c.Contains("EML") || c.Contains("RCPT"));
    }

    // Receiving (R4)

    [Fact]
    [Trait("Spec", "RQC-2")]
    public async Task ReceivingWithAnIntactSeal_RestocksEveryLineAsSellable_AndCompletesTheCase()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2, 1);

        var detail = (await host.SendAsync(Receive(id, true)))!;

        Assert.Equal(("COMPLETED", true, Bin), (detail.Status, detail.SealIntact, detail.ReceivingBinCode));
        Assert.All(detail.Lines, l => Assert.Equal("RESTOCK", l.Decision));
        Assert.Equal([2, 1], host.Stock.Posted.Values.Select(p => p.Quantity));
        Assert.All(host.Stock.Posted.Values, p => Assert.False(p.Seconds));
        Assert.Single(await OutboxPayloadsAsync(detail.OrderId));
    }

    [Fact]
    [Trait("Spec", "RQC-3")]
    public async Task ReceivingWithoutAnIntactSeal_ChangesNoStock_AndWaitsForInspection()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);

        var detail = (await host.SendAsync(Receive(id, false)))!;

        Assert.Equal("IN_INSPECTION", detail.Status);
        Assert.Equal(0, host.Stock.Calls);
        Assert.Empty(await OutboxPayloadsAsync(detail.OrderId));
    }

    [Fact]
    [Trait("Spec", "RQC-9")]
    public async Task ReceivingTwice_IsRefused_AndMovesNoMoreStock()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);
        await host.SendAsync(Receive(id, true));
        var calls = host.Stock.Calls;

        var refusal = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Receive(id, true)));

        Assert.Equal("This parcel was already received.", refusal.Message);
        Assert.Equal(calls, host.Stock.Calls);
    }

    [Fact]
    [Trait("Spec", "RQC-10")]
    public async Task ReceivingIntoAnUnknownBin_IsRefused_AndTheCaseStaysAsItWas()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Receive(id, true, "NO-SUCH")));

        Assert.Contains("does not exist", refusal.Message);
        var detail = await host.DetailAsync(id);
        Assert.Equal("AWAITING_RECEIPT", detail.Status);
        Assert.All(detail.Lines, l => Assert.Null(l.Decision));
        Assert.Empty(host.Stock.Posted);

        // The same case can still be received once the bin is right (the failed try left nothing behind).
        Assert.Equal("COMPLETED", (await host.SendAsync(Receive(id, true)))!.Status);
    }

    [Fact]
    [Trait("Spec", "RQC-12")]
    public async Task AnUnknownCase_IsNotFound()
    {
        using var host = new ReturnsHost(postgres);

        Assert.Null(await host.SendAsync(Receive(987_654_321, true)));
        Assert.Null(await host.SendAsync(new GetReturnCaseQuery(987_654_321)));
        Assert.Null(await host.SendAsync(new CompleteReturnCaseCommand(987_654_321, Staff)));
    }

    // Deciding (R5)

    [Fact]
    [Trait("Spec", "RQC-4")]
    public async Task Restock_AndSeconds_PutTheQuantityInTheBin_OnceEach()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(3, 2);
        var lines = (await host.SendAsync(Receive(id, false)))!.Lines;

        await host.SendAsync(Decide(id, lines[0].Id, "RESTOCK", 3));
        var detail = (await host.SendAsync(Decide(id, lines[1].Id, "SECONDS", 1)))!;

        Assert.Equal(["RESTOCK", "SECONDS"], detail.Lines.Select(l => l.Decision));
        Assert.Equal([3, 1], detail.Lines.Select(l => l.DecidedQuantity));
        Assert.Equal([(3, false), (1, true)], host.Stock.Posted.Values.Select(p => (p.Quantity, p.Seconds)));
        Assert.Equal(Bin, host.Stock.Posted.Values.First().BinCode);
        Assert.Equal(Staff, detail.Lines[0].DecidedBy);
    }

    [Fact]
    [Trait("Spec", "RQC-5")]
    public async Task Reject_NeedsANote_ChangesNoStock_AndMarksTheLine()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);
        var line = (await host.SendAsync(Receive(id, false)))!.Lines[0];

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, line.Id, "REJECT", 2)));
        var detail = (await host.SendAsync(Decide(id, line.Id, "REJECT", 2, "Worn and washed")))!;

        Assert.Equal(("REJECT", "Worn and washed"), (detail.Lines[0].Decision, detail.Lines[0].Note));
        Assert.Equal(0, host.Stock.Calls);
    }

    [Theory]
    [Trait("Spec", "RQC-6")]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task ADecidedQuantity_MustBeFromOneToWhatWasOrdered(int quantity)
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);
        var line = (await host.SendAsync(Receive(id, false)))!.Lines[0];

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, line.Id, "RESTOCK", quantity)));

        Assert.Equal(0, host.Stock.Calls);
        Assert.Null((await host.DetailAsync(id)).Lines[0].Decision);
    }

    [Fact]
    [Trait("Spec", "RQC-9")]
    public async Task ALineDecidedTwice_IsRefused_AndMovesNoMoreStock()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);
        var line = (await host.SendAsync(Receive(id, false)))!.Lines[0];
        await host.SendAsync(Decide(id, line.Id, "RESTOCK", 2));
        var calls = host.Stock.Calls;

        var refusal = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, line.Id, "SECONDS", 2)));

        Assert.Equal("This line was already decided.", refusal.Message);
        Assert.Equal(calls, host.Stock.Calls);
    }

    [Fact]
    [Trait("Spec", "RQC-10")]
    public async Task WhenRestockingFails_TheLineStaysUndecided_AndCanBeDecidedLater()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);
        var line = (await host.SendAsync(Receive(id, false)))!.Lines[0];
        host.Stock.Bins.Clear();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, line.Id, "RESTOCK", 2)));

        Assert.Contains("does not exist", refusal.Message);
        Assert.Null((await host.DetailAsync(id)).Lines[0].Decision);
        host.Stock.Bins.Add(Bin);
        Assert.Equal("RESTOCK", (await host.SendAsync(Decide(id, line.Id, "RESTOCK", 2)))!.Lines[0].Decision);
        Assert.Single(host.Stock.Posted);
    }

    [Fact]
    [Trait("Spec", "RQC-6")]
    public async Task ALineFromAnotherCase_OrAnUnknownDecision_IsRefused()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2);
        var otherLine = (await host.SendAsync(Receive(await host.OpenCaseAsync(1), false)))!.Lines[0];
        await host.SendAsync(Receive(id, false));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, otherLine.Id, "RESTOCK", 1)));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, otherLine.Id, "BURN", 1)));
    }

    // Completing (R6)

    [Fact]
    [Trait("Spec", "RQC-7")]
    public async Task Completing_WithLinesWaiting_IsRefusedAndSaysHowMany()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(1, 1);
        var lines = (await host.SendAsync(Receive(id, false)))!.Lines;
        await host.SendAsync(Decide(id, lines[0].Id, "RESTOCK", 1));

        var refusal = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompleteReturnCaseCommand(id, Staff)));

        Assert.Equal("1 line is still waiting for a decision.", refusal.Message);
        Assert.Equal("IN_INSPECTION", (await host.DetailAsync(id)).Status);
    }

    [Fact]
    [Trait("Spec", "RQC-8")]
    public async Task Completing_FreezesTheCase_AndAnnouncesTheResultOfEachLineOnce()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(2, 1);
        var lines = (await host.SendAsync(Receive(id, false)))!.Lines;
        await host.SendAsync(Decide(id, lines[0].Id, "SECONDS", 2));
        var orderId = (await host.DetailAsync(id)).OrderId;
        Assert.Empty(await OutboxPayloadsAsync(orderId));
        await host.SendAsync(Decide(id, lines[1].Id, "REJECT", 1, "Not the item ordered"));
        Assert.Empty(await OutboxPayloadsAsync(orderId));

        var detail = (await host.SendAsync(new CompleteReturnCaseCommand(id, Staff)))!;

        Assert.Equal(("COMPLETED", Staff), (detail.Status, detail.CompletedBy));
        var payloads = await OutboxPayloadsAsync(orderId);
        var announced = JsonSerializer.Deserialize<ReturnQcCompletedEvent>(Assert.Single(payloads))!;
        Assert.Equal([("SECONDS", 2), ("REJECT", 1)], announced.Lines.Select(l => (l.DecisionCode, l.Quantity)));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CompleteReturnCaseCommand(id, Staff)));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Decide(id, lines[0].Id, "RESTOCK", 1)));
        Assert.Single(await OutboxPayloadsAsync(orderId));
    }

    // Queries (R7)

    [Fact]
    [Trait("Spec", "RQC-11")]
    public async Task TheList_IsPaged_FilteredByStatus_NewestFirst_AndRunsAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new ReturnsHost(postgres, counter);
        var open = new List<long>();
        for (var i = 0; i < 4; i++)
        {
            open.Add(await host.OpenCaseAsync(1, 1));
        }

        await host.SendAsync(Receive(open[0], false));

        counter.Reset();
        var all = await host.SendAsync(new ListReturnCasesQuery(null, 1, 2));
        var queriesForTwo = counter.Count;
        var waiting = await host.SendAsync(new ListReturnCasesQuery("AWAITING_RECEIPT", 1, 100));
        var inspecting = await host.SendAsync(new ListReturnCasesQuery("IN_INSPECTION", 1, 100));
        counter.Reset();
        await host.SendAsync(new ListReturnCasesQuery(null, 1, 100));
        var queriesForMany = counter.Count;

        Assert.Equal(2, all.Items.Count);
        Assert.True(all.TotalCount >= 4);
        Assert.True(all.Items[0].Id > all.Items[1].Id);
        Assert.DoesNotContain(waiting.Items, c => c.Id == open[0]);
        Assert.Contains(waiting.Items, c => c.Id == open[1]);
        Assert.Contains(inspecting.Items, c => c.Id == open[0] && c.LineCount == 2 && c.DecidedCount == 0);
        Assert.Equal(queriesForTwo, queriesForMany);
    }

    [Fact]
    [Trait("Spec", "RQC-11")]
    public async Task TheDetail_ShowsLinesAndWhoDidWhatAndWhen()
    {
        using var host = new ReturnsHost(postgres);
        var id = await host.OpenCaseAsync(1);

        var detail = (await host.SendAsync(Receive(id, true)))!;

        Assert.Equal((Staff, Staff), (detail.ReceivedBy, detail.CompletedBy));
        Assert.NotNull(detail.ReceivedAt);
        Assert.NotNull(detail.Lines[0].DecidedAt);
        Assert.Equal(Staff, detail.Lines[0].DecidedBy);
    }
}
