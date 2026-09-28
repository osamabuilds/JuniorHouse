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

            // SCRUM-93 task 6: additive dispatcher/lease/retry/dead-letter columns (AC-52) - safe
            // defaults so Sprint 1's existing unprocessed rows dispatch normally once the
            // dispatcher (task 7+) starts reading them.
            builder.Property(m => m.AggregateType).HasColumnName("AGGR_TYP").HasMaxLength(50).IsRequired().HasDefaultValue("PurchaseOrder");
            builder.Property(m => m.AggregateId).HasColumnName("AGGR_ID");
            builder.Property(m => m.MessageVersion).HasColumnName("MSG_VER").IsRequired().HasDefaultValue((short)1);
            builder.Property(m => m.AttemptCount).HasColumnName("ATMP_CNT").IsRequired().HasDefaultValue((short)0);
            builder.Property(m => m.NextAttemptDte).HasColumnName("NXT_ATMP_DTE");
            builder.Property(m => m.ClaimedBy).HasColumnName("CLM_BY").HasMaxLength(100);
            builder.Property(m => m.LeaseExpiryDte).HasColumnName("LEAS_EXPY_DTE");
            builder.Property(m => m.ProcessedDte).HasColumnName("PROC_DTE");
            builder.Property(m => m.IsDeadLettered).HasColumnName("DEDL_IND").IsRequired().HasDefaultValue(false);
            builder.Property(m => m.DeadLetterReason).HasColumnName("DEDL_RSN").HasMaxLength(500);
        });
    }
}
