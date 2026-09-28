using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

public sealed class AcknowledgePurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_SentPo_TransitionsAndWritesHistoryAndOutboxEvent()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var acknowledged = await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        Assert.Equal(3, acknowledged.StatusId); // PoStatus.Acknowledged
        Assert.Equal(3, acknowledged.StatusHistory.Count);

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        Assert.Contains(dbContext.Set<Romp.BuildingBlocks.Persistence.OutboxMessage>(), m => m.EventType == "PoAcknowledgedEvent");
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task Handle_EmptyBody_DelegatesToRecordVendorResponse()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var acknowledged = await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        Assert.Equal(3, acknowledged.StatusId);
        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var communication = Assert.Single(dbContext.Set<PoVendorCommunication>().Where(c => c.PoId == po.Id));
        Assert.Equal(1, communication.CommunicationTypeId);    // Confirmed
        Assert.Equal(5, communication.ChannelId); // Unspecified
    }
}
