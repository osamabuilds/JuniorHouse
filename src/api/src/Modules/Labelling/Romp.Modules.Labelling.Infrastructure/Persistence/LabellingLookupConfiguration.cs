using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Labelling.Domain;

namespace Romp.Modules.Labelling.Infrastructure.Persistence;

/// <summary>
/// The system-owned Labelling lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are the stable keys
/// that code refers to. Printers are lookup-shaped configuration rows too: no endpoint can add or change one (PJ-12).
/// </summary>
internal static class LabellingLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<GtinStatusLookup>(modelBuilder, "GTIN_STS_LKP",
            (GtinStatus.Active, "Active", "In use on tags"),
            (GtinStatus.Retired, "Retired", "Voided and never issued again"));

        Configure<VoidReasonLookup>(modelBuilder, "VOID_RSN_LKP",
            (VoidReasons.Error, "Error", "Assigned in error"),
            (VoidReasons.Recalled, "Recalled", "Product recalled"),
            (VoidReasons.Other, "Other", "Other reason"));

        Configure<LabelWorkStatusLookup>(modelBuilder, "LBL_WORK_STS_LKP",
            (LabelWorkStatus.Ready, "Ready", "Ready to print"),
            (LabelWorkStatus.NeedsAttention, "NeedsAttention", "Needs attention before it can be printed"));

        Configure<ParkReasonLookup>(modelBuilder, "PARK_RSN_LKP",
            (ParkReasons.NoStyle, "NoStyle", "The PO's style could not be found"),
            (ParkReasons.StyleRetired, "StyleRetired", "The style is retired; confirm to label the goods"),
            (ParkReasons.UnknownCode, "UnknownCode", "A colour or size code could not be found"));

        Configure<LabelOutcomeLookup>(modelBuilder, "LBL_OUTC_LKP",
            (LabelOutcome.Accepted, "Accepted", "Accepted by QC"),
            (LabelOutcome.Concession, "Concession", "Accepted with concession (2ND mark)"));

        Configure<PrintJobStatusLookup>(modelBuilder, "PRT_JOB_STS_LKP",
            (PrintJobStatus.Queued, "Queued", "Waiting for the printer"),
            (PrintJobStatus.Printing, "Printing", "Being printed"),
            (PrintJobStatus.Printed, "Printed", "Printed"),
            (PrintJobStatus.Failed, "Failed", "Failed after all attempts"),
            (PrintJobStatus.Cancelled, "Cancelled", "Cancelled"));

        Configure<PrintJobKindLookup>(modelBuilder, "PRT_JOB_KIND_LKP",
            (PrintJobKind.Original, "Original", "First printing for accepted stock"),
            (PrintJobKind.Reprint, "Reprint", "Reprint of an existing tag"));

        Configure<ReprintReasonLookup>(modelBuilder, "RPRT_RSN_LKP",
            (ReprintReasons.Lost, "Lost", "Tag lost"),
            (ReprintReasons.Damaged, "Damaged", "Tag damaged"),
            (ReprintReasons.PriceChange, "PriceChange", "Price changed"),
            (ReprintReasons.Other, "Other", "Other reason"));

        Configure<PrintFormatLookup>(modelBuilder, "PRT_FMT_LKP",
            (PrintFormat.Zpl, "Zpl", "ZPL label printer"),
            (PrintFormat.Pdf, "Pdf", "PDF (development preview printer)"));

        Configure<AttemptOutcomeLookup>(modelBuilder, "PRT_ATMP_OTCM_LKP",
            (AttemptOutcome.Printed, "Printed", "The printer accepted the job"),
            (AttemptOutcome.Failed, "Failed", "The attempt failed"));

        ConfigurePrinters(modelBuilder);
    }

    private static void ConfigurePrinters(ModelBuilder modelBuilder)
    {
        var builder = ConfigureShape<PrinterLookup>(modelBuilder, "PRTR_LKP");
        builder.Property(p => p.FormatId).HasColumnName("PRT_FMT_ID").IsRequired();
        builder.Property(p => p.Address).HasColumnName("ADDR").HasMaxLength(200).IsRequired();
        builder.HasOne<PrintFormatLookup>().WithMany().HasForeignKey(p => p.FormatId).HasConstraintName("FK_PRTR_LKP_PRT_FMT_LKP");

        // Development printers (ADR 0010, D7): the PDF "printer" renders and discards; the jam printer always fails so the
        // retry path can be seen. Both are refused unless Labelling:AllowFakePrinters is on (Development and Testing only).
        builder.HasData(
            new { Id = (short)1, Code = "FAKE-PDF", Name = "Preview printer (no hardware)", Description = (string?)"Renders the tag as a PDF and discards it.", SortSeq = (short)1, IsActive = true, FormatId = PrintFormat.Pdf, Address = "memory" },
            new { Id = (short)2, Code = "FAKE-JAM", Name = "Jammed printer (always fails)", Description = (string?)"For trying the retry path.", SortSeq = (short)2, IsActive = true, FormatId = PrintFormat.Pdf, Address = "jam" });
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name)[] rows)
        where TLookup : LabellingLookup
    {
        var builder = ConfigureShape<TLookup>(modelBuilder, tableName);
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)null, SortSeq = r.Id, IsActive = true,
        }).ToArray());
    }

    private static EntityTypeBuilder<TLookup> ConfigureShape<TLookup>(ModelBuilder modelBuilder, string tableName)
        where TLookup : LabellingLookup
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        return builder;
    }
}
