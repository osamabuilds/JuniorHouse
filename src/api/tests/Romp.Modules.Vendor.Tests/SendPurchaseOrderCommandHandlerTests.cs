using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

public sealed class SendPurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Handle_DraftPo_TransitionsAndWritesHistoryAndOutboxEvent()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var sent = await sender.Send(new SendPurchaseOrderCommand(po.Id));

        Assert.Equal(2, sent.StatusId); // PoStatus.SentToVendor
        Assert.Equal(2, sent.StatusHistory.Count);
        Assert.Equal(1, sent.StatusHistory.First().PoStatusId);
        Assert.Equal(2, sent.StatusHistory.Last().PoStatusId);

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        Assert.Contains(dbContext.Set<Romp.BuildingBlocks.Persistence.OutboxMessage>(), m => m.EventType == "PoSentToVendorEvent");
    }
}
