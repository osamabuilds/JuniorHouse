using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Reference.Domain;
using Romp.Modules.Reference.Domain.Lookups;

namespace Romp.Modules.Reference.Infrastructure.Persistence;

/// <summary>
/// Applies docs/db/naming.md's standard lookup shape (ID smallint PK, CODE unique varchar(30),
/// NAME varchar(100), DSCR varchar(500) null, SORT_SEQ smallint, ACT_IND boolean) to any
/// <see cref="Lookup"/> subtype, so each of the eleven REF tables configures itself in one line
/// instead of repeating the same five column mappings.
/// </summary>
internal static class LookupModelBuilderExtensions
{
    public static EntityTypeBuilder<TLookup> ConfigureLookup<TLookup>(this ModelBuilder modelBuilder, string tableName)
        where TLookup : Lookup
    {
        var builder = modelBuilder.Entity<TLookup>();

        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();

        return builder;
    }
}
