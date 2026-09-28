using Microsoft.EntityFrameworkCore;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Maps <see cref="InboxMessage"/> to <c>INBX</c> in the calling module's own schema
/// (docs/db/naming.md section 4: "Inbox (per module) - the module's own schema, table INBX").
/// A module calls this once from its DbContext's OnModelCreating when it registers its first
/// handler with a database effect (SCRUM-93 task 12; none this sprint - see task 13's placeholder).
/// </summary>
public static class InboxModelBuilderExtensions
{
    public static void HasInboxTable(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("INBX", schema);
            builder.HasKey(m => new { m.MessageId, m.HandlerName });
            builder.Property(m => m.MessageId).HasColumnName("MSG_ID");
            builder.Property(m => m.HandlerName).HasColumnName("HNDL_NAME").HasMaxLength(200);
            builder.Property(m => m.ProcessedDte).HasColumnName("PROC_DTE");
        });
    }
}
