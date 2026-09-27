using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Domain;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Applies docs/db/naming.md's standard shape for a transactional table's audit + concurrency
/// columns, so every module's entity configuration writes one line instead of repeating five
/// column mappings.
/// </summary>
public static class AuditableEntityTypeBuilderExtensions
{
    public static void HasAuditColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IAuditable
    {
        builder.Property(e => e.InsrDte).HasColumnName("INSR_DTE").IsRequired();
        builder.Property(e => e.InsrBy).HasColumnName("INSR_BY").HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdtDte).HasColumnName("UPDT_DTE");
        builder.Property(e => e.UpdtBy).HasColumnName("UPDT_BY").HasMaxLength(100);

        // PostgreSQL's own row-version system column, mapped as an EF shadow property - no
        // application-managed version column to keep in sync (docs/db/naming.md section 2).
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
