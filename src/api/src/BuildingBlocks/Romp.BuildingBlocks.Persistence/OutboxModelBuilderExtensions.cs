using Microsoft.EntityFrameworkCore;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Maps <see cref="OutboxMessage"/> to <c>OUTB_MSG</c> in the calling module's own schema
/// (docs/db/naming.md section 4: "Outbox (per module) - the module's own schema, table OUTB_MSG").
/// Each module that raises domain events calls this once from its DbContext's OnModelCreating.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    public static void HasOutboxTable(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OUTB_MSG", schema);
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).HasColumnName("ID");
            builder.Property(m => m.EventType).HasColumnName("EVNT_TYP").HasMaxLength(200).IsRequired();
            builder.Property(m => m.Payload).HasColumnName("PYLD").HasColumnType("jsonb").IsRequired();
            builder.Property(m => m.InsrDte).HasColumnName("INSR_DTE").IsRequired();
        });
    }
}
