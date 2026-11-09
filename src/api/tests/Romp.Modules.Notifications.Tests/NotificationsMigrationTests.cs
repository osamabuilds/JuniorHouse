using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests;

public sealed class NotificationsMigrationTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    [Fact]
    [Trait("Spec", "DLV-1")]
    public async Task Migrate_CreatesTheNtfySchemaWithItsOutboxAndInbox()
    {
        await using var context = postgres.NewContext();
        var tables = await context.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'NTFY'")
            .ToListAsync();

        Assert.Contains("OUTB_MSG", tables);
        Assert.Contains("INBX", tables);
    }
}

public sealed class NotificationsPersistenceTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static Notification New(Guid eventId, NotificationKind kind = NotificationKind.OrderConfirmation) =>
        new(kind, ReferenceKinds.Order, 42, eventId, Now, TimeSpan.FromDays(7), 1);

    [Fact]
    [Trait("Spec", "DLV-4")]
    public async Task ANotificationWithItsDeliveries_RoundTrips()
    {
        var eventId = Guid.NewGuid();
        await using (var context = postgres.NewContext())
        {
            var notification = New(eventId);
            notification.Plan(ChannelPlan.For(true, false), Now);
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        var loaded = await read.Notifications.Include(n => n.Deliveries).SingleAsync(n => n.EventId == eventId);
        Assert.Equal(NotificationKind.OrderConfirmation, loaded.Kind);
        Assert.Equal(ReferenceKinds.Order, loaded.ReferenceKind);
        Assert.Equal(2, loaded.Deliveries.Count);
        Assert.All(loaded.Deliveries, d => Assert.Equal(DeliveryStatus.Queued, d.Status));
    }

    [Fact]
    [Trait("Spec", "DLV-4")]
    public async Task TheSameEventAndKind_CannotCreateASecondNotification()
    {
        var eventId = Guid.NewGuid();
        await using (var context = postgres.NewContext())
        {
            context.Notifications.Add(New(eventId));
            await context.SaveChangesAsync();
        }

        await using var again = postgres.NewContext();
        again.Notifications.Add(New(eventId));
        await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());

        // A different kind for the same event is a different notification.
        await using var other = postgres.NewContext();
        other.Notifications.Add(New(eventId, NotificationKind.OrderCancelled));
        await other.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "DLV-4")]
    public async Task TheLookupTables_AreSeededWithTheCodesTheCodeUses()
    {
        await using var context = postgres.NewContext();

        var statuses = await context.Database.SqlQuery<string>($"SELECT \"CODE\" AS \"Value\" FROM \"NTFY\".\"CHNL_STS_LKP\" ORDER BY \"ID\"").ToListAsync();
        var channels = await context.Database.SqlQuery<string>($"SELECT \"CODE\" AS \"Value\" FROM \"NTFY\".\"CHNL_LKP\" ORDER BY \"ID\"").ToListAsync();

        Assert.Equal(["QUED", "SENT", "DLVD", "FAIL", "DEAD", "EXPR", "SKIP"], statuses);
        Assert.Equal(["EMAL", "WHTS"], channels);
    }
}
