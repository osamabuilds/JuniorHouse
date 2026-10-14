using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Production.Tests.Support;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Tests.ProductionRuns;

public sealed class OpenProductionRunTests
{
    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Handle_AcknowledgedPo_OpensOneRunWithTheFabricResponsibilityAndSendDate()
    {
        using var host = new TestHost(nameof(Handle_AcknowledgedPo_OpensOneRunWithTheFabricResponsibilityAndSendDate));

        var runId = await host.OpenRunAsync();

        var run = await host.WithDbAsync(db => db.Runs.AsNoTracking().SingleAsync(r => r.Id == runId));
        Assert.Equal(FakePurchaseOrderQueries.PoId, run.PoId);
        Assert.Equal(FakePurchaseOrderQueries.PoNo, run.PoNo);
        Assert.Equal(FabricResponsibility.VendorSupplied, run.FabricResponsibilityId);
        Assert.Equal(FakePurchaseOrderQueries.SentDate, run.PoSentDate);
        Assert.Equal(new DateOnly(2026, 10, 25), run.LatestAcceptableDate);
        Assert.Equal(RunStatus.Open, run.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Handle_CalledTwice_StillExactlyOneRun()
    {
        using var host = new TestHost(nameof(Handle_CalledTwice_StillExactlyOneRun));

        var first = await host.OpenRunAsync();
        var second = await host.OpenRunAsync();

        Assert.Equal(first, second);
        Assert.Equal(1, await host.WithDbAsync(db => db.Runs.CountAsync()));
    }

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData(2)] // Sent to vendor
    [InlineData(4)] // Cancelled
    public async Task Handle_NonAcknowledgedPo_NoRun(short statusId)
    {
        using var host = new TestHost($"{nameof(Handle_NonAcknowledgedPo_NoRun)}{statusId}");
        host.PoQueries.Context = host.PoQueries.Context! with { StatusId = statusId };

        var result = await host.SendAsync(new OpenProductionRunCommand(FakePurchaseOrderQueries.PoId));

        Assert.Null(result);
        Assert.Equal(0, await host.WithDbAsync(db => db.Runs.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Handle_PoNeverSent_NoRun()
    {
        using var host = new TestHost(nameof(Handle_PoNeverSent_NoRun));
        host.PoQueries.Context = null;

        Assert.Null(await host.SendAsync(new OpenProductionRunCommand(FakePurchaseOrderQueries.PoId)));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Record_OnARunThatDoesNotExist_NotFound()
    {
        using var host = new TestHost(nameof(Record_OnARunThatDoesNotExist_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetProductionRunQuery(12345)));
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-30")]
    public async Task Handle_WritesTheOpenedEventToTheOutboxInTheSameSave()
    {
        using var host = new TestHost(nameof(Handle_WritesTheOpenedEventToTheOutboxInTheSameSave));

        await host.OpenRunAsync();

        var types = await host.WithDbAsync(db => db.Set<OutboxMessage>().Select(m => m.EventType).ToListAsync());
        Assert.Equal(["ProductionRunOpenedEvent"], types);
    }
}

public sealed class PoEventConsumerTests
{
    private static async Task<T> InScopeAsync<T>(TestHost host, long messageId, Func<IServiceProvider, Task<T>> action) =>
        await host.WithScopeAsync(sp =>
        {
            var context = sp.GetRequiredService<OutboxMessageContext>();
            context.MessageId = messageId;
            context.Schema = "VNDR";
            return action(sp);
        });

    private static Task<bool> AcknowledgedAsync(TestHost host, long messageId) =>
        InScopeAsync(host, messageId, async sp =>
        {
            var consumer = sp.GetServices<IOutboxMessageHandler<PoAcknowledgedEvent>>().Single(h => h.GetType().Name == "PoAcknowledgedConsumer");
            await consumer.HandleAsync(new PoAcknowledgedEvent(FakePurchaseOrderQueries.PoId, FakePurchaseOrderQueries.PoNo), CancellationToken.None);
            return true;
        });

    private static TestHost NewHost(string name) => new(name, services =>
    {
        // Mirrors ProductionModule's registrations of the consumers (they are internal to Infrastructure).
        var type = typeof(ProductionDbContext).Assembly.GetTypes();
        void Register<TEvent>(string consumerName) => services.AddScoped(
            typeof(IOutboxMessageHandler<TEvent>), type.Single(t => t.Name == consumerName));

        Register<PoAcknowledgedEvent>("PoAcknowledgedConsumer");
        Register<PoCancelledEvent>("PoCancelledConsumer");
        Register<PoClosedEvent>("PoClosedConsumer");
        Register<PoRevisionPutInForceEvent>("PoRevisionPutInForceConsumer");
    });

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task PoAcknowledged_TwiceSameMessage_OneRunAndOneInboxRow()
    {
        using var host = NewHost(nameof(PoAcknowledged_TwiceSameMessage_OneRunAndOneInboxRow));

        await AcknowledgedAsync(host, messageId: 41);
        await AcknowledgedAsync(host, messageId: 41);

        Assert.Equal(1, await host.WithDbAsync(db => db.Runs.CountAsync()));
        var inbox = await host.WithDbAsync(db => db.Set<InboxMessage>().ToListAsync());
        var row = Assert.Single(inbox);
        Assert.Equal(41, row.MessageId);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task PoAcknowledged_DifferentMessagesForTheSamePo_StillOneRun()
    {
        using var host = NewHost(nameof(PoAcknowledged_DifferentMessagesForTheSamePo_StillOneRun));

        await AcknowledgedAsync(host, messageId: 1);
        await AcknowledgedAsync(host, messageId: 2);

        Assert.Equal(1, await host.WithDbAsync(db => db.Runs.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public async Task PoCancelled_ClosesRunAsCancelled_AndARedeliveryIsANoOp()
    {
        using var host = NewHost(nameof(PoCancelled_ClosesRunAsCancelled_AndARedeliveryIsANoOp));
        await AcknowledgedAsync(host, 1);

        for (var i = 0; i < 2; i++)
        {
            await InScopeAsync(host, 2, async sp =>
            {
                var consumer = sp.GetServices<IOutboxMessageHandler<PoCancelledEvent>>().Single(h => h.GetType().Name == "PoCancelledConsumer");
                await consumer.HandleAsync(new PoCancelledEvent(FakePurchaseOrderQueries.PoId, FakePurchaseOrderQueries.PoNo, 3), CancellationToken.None);
                return true;
            });
        }

        var run = await host.WithDbAsync(db => db.Runs.AsNoTracking().SingleAsync());
        Assert.Equal(RunStatus.Cancelled, run.StatusId);
        var cancelledEvents = await host.WithDbAsync(db => db.Set<OutboxMessage>().CountAsync(m => m.EventType == "ProductionRunCancelledEvent"));
        Assert.Equal(1, cancelledEvents);
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public async Task PoClosed_ClosesTheRun()
    {
        using var host = NewHost(nameof(PoClosed_ClosesTheRun));
        await AcknowledgedAsync(host, 1);

        await InScopeAsync(host, 2, async sp =>
        {
            var consumer = sp.GetServices<IOutboxMessageHandler<PoClosedEvent>>().Single(h => h.GetType().Name == "PoClosedConsumer");
            await consumer.HandleAsync(new PoClosedEvent(FakePurchaseOrderQueries.PoId, FakePurchaseOrderQueries.PoNo), CancellationToken.None);
            return true;
        });

        Assert.Equal(RunStatus.Closed, (await host.WithDbAsync(db => db.Runs.AsNoTracking().SingleAsync())).StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public async Task RevisionPutInForce_UpdatesLatestAcceptableDateCopy()
    {
        using var host = NewHost(nameof(RevisionPutInForce_UpdatesLatestAcceptableDateCopy));
        await AcknowledgedAsync(host, 1);
        var terms = FakePurchaseOrderQueries.DefaultTerms(revision: 1, latest: new DateOnly(2026, 12, 1));

        await InScopeAsync(host, 2, async sp =>
        {
            var consumer = sp.GetServices<IOutboxMessageHandler<PoRevisionPutInForceEvent>>().Single(h => h.GetType().Name == "PoRevisionPutInForceConsumer");
            var snapshot = new PoTermsSnapshot(
                terms.UnitCost, terms.ExpectedDeliveryDate, terms.LatestAcceptableDate, terms.OverTolerancePercent, terms.UnderTolerancePercent,
                terms.PaymentTermId, terms.AdvancePercent, terms.FabricResponsibilityId, []);
            await consumer.HandleAsync(new PoRevisionPutInForceEvent(FakePurchaseOrderQueries.PoId, FakePurchaseOrderQueries.PoNo, 1, 1, snapshot, false), CancellationToken.None);
            return true;
        });

        Assert.Equal(new DateOnly(2026, 12, 1), (await host.WithDbAsync(db => db.Runs.AsNoTracking().SingleAsync())).LatestAcceptableDate);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task PoAcknowledged_WhenThePoIsNotYetAcknowledged_NoRun_ThenALaterMessageOpensIt()
    {
        using var host = NewHost(nameof(PoAcknowledged_WhenThePoIsNotYetAcknowledged_NoRun_ThenALaterMessageOpensIt));
        host.PoQueries.Context = host.PoQueries.Context! with { StatusId = 2 };

        await AcknowledgedAsync(host, 5);
        Assert.Equal(0, await host.WithDbAsync(db => db.Runs.CountAsync()));

        // The message was processed (nothing to do) and is recorded; a later, different message opens the run once the PO is acknowledged.
        host.PoQueries.Context = new PoProductionContext(FakePurchaseOrderQueries.PoId, FakePurchaseOrderQueries.PoNo, 3, FakePurchaseOrderQueries.SentDate, 1);
        await AcknowledgedAsync(host, 6);
        Assert.Equal(1, await host.WithDbAsync(db => db.Runs.CountAsync()));
    }
}
