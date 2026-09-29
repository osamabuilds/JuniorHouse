using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Domain;

namespace Romp.Modules.Quality.Infrastructure.Persistence;

/// <summary>The thirteen system-owned QC lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are the stable keys code refers to.</summary>
internal static class QualityLookupConfiguration
{
    // The special inspection levels have no seeded sampling plan yet, so they are retired (ACT_IND false) rather than offered and failing.
    private static readonly HashSet<string> SpecialLevelsWithoutPlan = ["S1", "S2", "S3", "S4"];

    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<GrnStatusLookup>(modelBuilder, "GRN_STS_LKP",
            (GrnStatus.Recorded, "Recorded", "Recorded"));

        Configure<GrnReconciliationLookup>(modelBuilder, "GRN_RCNL_LKP",
            (GrnReconciliation.Matched, "Matched", "Matched the delivery note"),
            (GrnReconciliation.Shortage, "Short", "Short of the delivery note"),
            (GrnReconciliation.Over, "Over", "Over the delivery note"));

        Configure<BatchStatusLookup>(modelBuilder, "QC_BTCH_STS_LKP",
            (BatchStatus.Pending, "Pending", "Pending inspection"),
            (BatchStatus.InInspection, "InInspection", "In inspection"),
            (BatchStatus.Decided, "Decided", "Decided"),
            (BatchStatus.AwaitingRework, "AwaitingRework", "Awaiting rework from the vendor"),
            (BatchStatus.Closed, "Closed", "Closed"));

        Configure<OutcomeLookup>(modelBuilder, "QC_OTCM_LKP",
            (QcOutcome.Accepted, "Accepted", "Accepted"),
            (QcOutcome.Concession, "Concession", "Accepted with concession"),
            (QcOutcome.Rejected, "Rejected", "Rejected and returned to the vendor"),
            (QcOutcome.Rework, "Rework", "Sent back to the vendor for rework"));

        Configure<InspectionModeLookup>(modelBuilder, "QC_INSP_MODE_LKP",
            (InspectionMode.Aql, "Aql", "AQL sampling"),
            (InspectionMode.Full, "Full", "100% inspection"));

        Configure<InspectionResultLookup>(modelBuilder, "QC_INSP_RSLT_LKP",
            (InspectionResult.Pass, "Pass", "Pass"),
            (InspectionResult.Fail, "Fail", "Fail"));

        Configure<InspectionLevelLookup>(modelBuilder, "QC_INSP_LVL_LKP",
            (InspectionLevel.S1, "S1", "Special level S-1"),
            (InspectionLevel.S2, "S2", "Special level S-2"),
            (InspectionLevel.S3, "S3", "Special level S-3"),
            (InspectionLevel.S4, "S4", "Special level S-4"),
            (InspectionLevel.I, "I", "General level I"),
            (InspectionLevel.II, "II", "General level II"),
            (InspectionLevel.III, "III", "General level III"));

        ConfigureAql(modelBuilder);

        Configure<CheckTypeLookup>(modelBuilder, "QC_CHK_TYP_LKP",
            (CheckType.Stitching, "Stitching", "Stitching"),
            (CheckType.Measurements, "Measurements", "Measurements against the size chart"),
            (CheckType.FabricColourMatch, "FabricColourMatch", "Fabric and colour match to the approved sample"),
            (CheckType.TrimsSecure, "TrimsSecure", "Trims, buttons and zips secure"),
            (CheckType.VisibleDefects, "VisibleDefects", "No visible defects"),
            (CheckType.CareLabel, "CareLabel", "Care label content correct"));

        Configure<CheckResultLookup>(modelBuilder, "QC_CHK_RSLT_LKP",
            (CheckResult.Pass, "Pass", "Pass"),
            (CheckResult.Fail, "Fail", "Fail"),
            (CheckResult.NotApplicable, "NotApplicable", "Not applicable"));

        Configure<SafetyTypeLookup>(modelBuilder, "QC_SAFE_TYP_LKP",
            (SafetyType.ChokingHazardTrims, "ChokingHazardTrims", "Choking hazard on small trims"),
            (SafetyType.DrawstringNeckline, "DrawstringNeckline", "Drawstring safety on necklines"));

        Configure<DefectTypeLookup>(modelBuilder, "QC_DFCT_TYP_LKP",
            (DefectType.Stitching, "Stitching", "Stitching"),
            (DefectType.Measurement, "Measurement", "Measurement"),
            (DefectType.FabricColour, "FabricColour", "Fabric or colour"),
            (DefectType.TrimsSecurity, "TrimsSecurity", "Trims not secure"),
            (DefectType.VisibleDefect, "VisibleDefect", "Visible defect"),
            (DefectType.CareLabel, "CareLabel", "Care label"),
            (DefectType.Other, "Other", "Other"));

        Configure<DefectSeverityLookup>(modelBuilder, "QC_DFCT_SVRT_LKP",
            (DefectSeverity.Critical, "Critical", "Critical"),
            (DefectSeverity.Major, "Major", "Major"),
            (DefectSeverity.Minor, "Minor", "Minor"));
    }

    private static void ConfigureAql(ModelBuilder modelBuilder)
    {
        var builder = ConfigureShape<AqlLevelLookup>(modelBuilder, "QC_AQL_LVL_LKP");
        builder.Property(l => l.AqlValue).HasColumnName("AQL_VAL").HasColumnType("numeric(4,2)").IsRequired();

        (short Id, string Code, string Name, decimal Value)[] rows =
        [
            (AqlLevel.Aql065, "Aql0_65", "AQL 0.65", 0.65m),
            (AqlLevel.Aql100, "Aql1_0", "AQL 1.0", 1.0m),
            (AqlLevel.Aql150, "Aql1_5", "AQL 1.5", 1.5m),
            (AqlLevel.Aql250, "Aql2_5", "AQL 2.5", 2.5m),
            (AqlLevel.Aql400, "Aql4_0", "AQL 4.0", 4.0m),
            (AqlLevel.Aql650, "Aql6_5", "AQL 6.5", 6.5m),
        ];
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)null, SortSeq = r.Id, IsActive = true, AqlValue = r.Value,
        }).ToArray());
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name)[] rows)
        where TLookup : QualityLookup
    {
        var builder = ConfigureShape<TLookup>(modelBuilder, tableName);
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)null, SortSeq = r.Id, IsActive = !SpecialLevelsWithoutPlan.Contains(r.Code),
        }).ToArray());
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TLookup> ConfigureShape<TLookup>(ModelBuilder modelBuilder, string tableName)
        where TLookup : QualityLookup
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
