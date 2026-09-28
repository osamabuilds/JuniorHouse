using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Application;
using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Infrastructure;

/// <summary>
/// EF Core context for the <c>REF</c> schema (shared lookups). Implements
/// <see cref="IReferenceDbContext"/> (Application's own abstraction) so Application-layer command
/// and query handlers can use it without depending on this Infrastructure project (ADR 0002).
/// </summary>
public sealed class ReferenceDbContext(DbContextOptions<ReferenceDbContext> options)
    : DbContext(options), IReferenceDbContext
{
    DbSet<TLookup> IReferenceDbContext.Lookups<TLookup>() => Set<TLookup>();

    async Task IReferenceDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("REF");

        modelBuilder.ConfigureLookup<SizeLookup>("SIZE_LKP");
        modelBuilder.ConfigureLookup<ColourLookup>("CLR_LKP");
        modelBuilder.ConfigureLookup<FabricLookup>("FBRC_LKP");
        modelBuilder.ConfigureLookup<GenderLookup>("GNDR_LKP");
        modelBuilder.ConfigureLookup<AgeBracketLookup>("AGE_BRKT_LKP");
        modelBuilder.ConfigureLookup<CityLookup>("CITY_LKP");
        modelBuilder.ConfigureLookup<VendorSpecialisationLookup>("VNDR_SPCL_LKP");
        modelBuilder.ConfigureLookup<PoStatusLookup>("PO_STS_LKP");
        modelBuilder.ConfigureLookup<PoCancelReasonLookup>("PO_CNCL_RSN_LKP");

        modelBuilder.ConfigureLookup<CategoryLookup>("CATG_LKP")
            .Property(c => c.ParentCategoryId).HasColumnName("PRNT_CATG_ID");
        modelBuilder.Entity<CategoryLookup>()
            .HasOne<CategoryLookup>()
            .WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .HasConstraintName("FK_CATG_LKP_PRNT_CATG_ID")
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CategoryLookup>().HasIndex(c => c.ParentCategoryId);

        modelBuilder.ConfigureLookup<PaymentTermLookup>("PAYM_TERM_LKP")
            .Property(p => p.DefaultAdvancePercent).HasColumnName("DFLT_ADV_PCT").HasColumnType("numeric(5,2)");

        // Sprint 2 (SCRUM-93)
        modelBuilder.ConfigureLookup<AmendmentReasonLookup>("AMND_RSN_LKP");
        modelBuilder.ConfigureLookup<VendorCommChannelLookup>("VNDR_COMM_CHNL_LKP");
        modelBuilder.ConfigureLookup<FabricResponsibilityLookup>("FBRC_RESP_LKP");
        modelBuilder.ConfigureLookup<RevisionStatusLookup>("PO_REV_STS_LKP");
        modelBuilder.ConfigureLookup<AmendmentInitiatorLookup>("AMND_INIT_LKP");
        modelBuilder.ConfigureLookup<PoVendorCommTypeLookup>("PO_VNDR_COMM_TYP_LKP");
        modelBuilder.ConfigureLookup<PoFileCategoryLookup>("PO_FILE_CATG_LKP")
            .Property(c => c.IsVendorVisible).HasColumnName("VNDR_VSBL_IND").IsRequired();

        ReferenceSeedData.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
