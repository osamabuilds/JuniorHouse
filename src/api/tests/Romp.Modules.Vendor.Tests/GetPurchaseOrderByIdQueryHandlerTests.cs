using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class GetPurchaseOrderByIdQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Handle_ReturnsStatusHistoryTimelineInOrder()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var reloaded = await sender.Send(new GetPurchaseOrderByIdQuery(po.Id));

        Assert.NotNull(reloaded);
        Assert.Equal(3, reloaded.StatusHistory.Count);
        Assert.Equal([1, 2, 3], reloaded.StatusHistory.Select(h => (int)h.PoStatusId));
        Assert.True(reloaded.StatusHistory.Zip(reloaded.StatusHistory.Skip(1), (a, b) => a.InsrDte <= b.InsrDte).All(x => x));
    }
}
