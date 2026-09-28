using Microsoft.EntityFrameworkCore;

namespace Romp.BuildingBlocks.Persistence.Tests;

/// <summary>
/// SCRUM-93 task 12: the INBX shape is a convention documented for Sprint 3's first real consumer
/// (plan.md's Data section) - not instantiated in any real schema this sprint, so this test applies
/// it to a throwaway schema and only inspects EF metadata. Building an EF model never opens a
/// connection (see Romp.ArchitectureTests.NamingConventionTests), so this needs no Testcontainer.
/// </summary>
public sealed class InboxConventionTests
{
    private sealed class InboxTestDbContext(DbContextOptions<InboxTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasInboxTable("TEST");
    }

    [Fact]
    [Trait("Spec", "AC-56")]
    [Trait("Spec", "NFR-FT-05")]
    public void ApplyInboxConfiguration_ProducesExpectedShape()
    {
        var options = new DbContextOptionsBuilder<InboxTestDbContext>()
            .UseNpgsql("Host=localhost;Database=romp;Username=romp;Password=x")
            .Options;

        using var context = new InboxTestDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(InboxMessage));

        Assert.NotNull(entityType);
        Assert.Equal("INBX", entityType!.GetTableName());
        Assert.Equal("TEST", entityType.GetSchema());

        var primaryKey = entityType.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal(
            ["MSG_ID", "HNDL_NAME"],
            primaryKey!.Properties.Select(p => p.GetColumnName()).ToArray());

        var messageId = entityType.FindProperty(nameof(InboxMessage.MessageId));
        Assert.NotNull(messageId);
        Assert.Equal("MSG_ID", messageId!.GetColumnName());
        Assert.Equal(typeof(long), messageId.ClrType);

        var handlerName = entityType.FindProperty(nameof(InboxMessage.HandlerName));
        Assert.NotNull(handlerName);
        Assert.Equal("HNDL_NAME", handlerName!.GetColumnName());
        Assert.Equal(200, handlerName.GetMaxLength());

        var processedDte = entityType.FindProperty(nameof(InboxMessage.ProcessedDte));
        Assert.NotNull(processedDte);
        Assert.Equal("PROC_DTE", processedDte!.GetColumnName());
        Assert.Equal(typeof(DateTimeOffset?), processedDte.ClrType);
    }
}
