using Microsoft.EntityFrameworkCore;
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
