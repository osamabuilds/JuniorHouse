using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.OrderConfirmation;

public sealed class NoAddressTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "OCF-8")]
    public async Task Run_WithNoEmail_RecordsAFailedDeliveryAndAnAttemptInsteadOfLosingIt()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender();
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        await using (var db = postgres.NewContext())
        {
            await OrderMessageKit.Processor(db, clock, sender, new FixedOrder(() => OrderMessageKit.Order(email: null))).RunOnceAsync(CancellationToken.None);
        }

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(ErrorCategory.NoAddress, delivery.LastError);
        Assert.Equal(1, await check.Attempts.CountAsync(a => a.DeliveryId == id));
        Assert.Equal(0, sender.Calls);
    }
}
