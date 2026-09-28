using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Infrastructure;

/// <summary>
/// Sprint 1 starter rows for every REF lookup (AC-1). The BRD gives exact business values only
/// for PO statuses and PO cancel reasons (spec Decisions); the general catalog lookups (sizes,
/// colours, fabrics, ...) have no BRD-specified list, so these are a reasonable starter set for a
/// Pakistani kidswear DTC brand - staff can add more through the admin Reference Data screen
/// (SCRUM-174) without a deployment, per naming.md's "design for growth" rule.
/// </summary>
internal static class ReferenceSeedData
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SizeLookup>().HasData(
            Row<SizeLookup>(1, "0-3M", "0-3 Months", 1),
            Row<SizeLookup>(2, "3-6M", "3-6 Months", 2),
            Row<SizeLookup>(3, "6-12M", "6-12 Months", 3),
            Row<SizeLookup>(4, "1-2Y", "1-2 Years", 4),
            Row<SizeLookup>(5, "2-3Y", "2-3 Years", 5),
            Row<SizeLookup>(6, "3-4Y", "3-4 Years", 6),
            Row<SizeLookup>(7, "4-5Y", "4-5 Years", 7),
            Row<SizeLookup>(8, "5-6Y", "5-6 Years", 8),
            Row<SizeLookup>(9, "6-7Y", "6-7 Years", 9),
            Row<SizeLookup>(10, "7-8Y", "7-8 Years", 10));

        modelBuilder.Entity<ColourLookup>().HasData(
            Row<ColourLookup>(1, "WHT", "White", 1),
            Row<ColourLookup>(2, "BLK", "Black", 2),
            Row<ColourLookup>(3, "RED", "Red", 3),
            Row<ColourLookup>(4, "PNK", "Pink", 4),
            Row<ColourLookup>(5, "BLU", "Blue", 5),
            Row<ColourLookup>(6, "NVY", "Navy", 6),
            Row<ColourLookup>(7, "YLW", "Yellow", 7),
            Row<ColourLookup>(8, "GRN", "Green", 8));

        modelBuilder.Entity<FabricLookup>().HasData(
            Row<FabricLookup>(1, "COTTON", "Cotton", 1),
            Row<FabricLookup>(2, "DENIM", "Denim", 2),
            Row<FabricLookup>(3, "FLEECE", "Fleece", 3),
            Row<FabricLookup>(4, "JERSEY", "Jersey", 4),
            Row<FabricLookup>(5, "LINEN", "Linen", 5),
            Row<FabricLookup>(6, "POLY", "Polyester", 6));

        modelBuilder.Entity<GenderLookup>().HasData(
            Row<GenderLookup>(1, "BOYS", "Boys", 1),
            Row<GenderLookup>(2, "GIRLS", "Girls", 2),
            Row<GenderLookup>(3, "UNISEX", "Unisex", 3));

        modelBuilder.Entity<AgeBracketLookup>().HasData(
            Row<AgeBracketLookup>(1, "NEWBORN", "Newborn (0-3M)", 1),
            Row<AgeBracketLookup>(2, "INFANT", "Infant (3-12M)", 2),
            Row<AgeBracketLookup>(3, "TODDLER", "Toddler (1-3Y)", 3),
            Row<AgeBracketLookup>(4, "KIDS", "Kids (4-8Y)", 4),
            Row<AgeBracketLookup>(5, "TWEEN", "Tween (9-12Y)", 5));

        modelBuilder.Entity<CityLookup>().HasData(
            Row<CityLookup>(1, "KHI", "Karachi", 1),
            Row<CityLookup>(2, "LHE", "Lahore", 2),
            Row<CityLookup>(3, "ISB", "Islamabad", 3),
            Row<CityLookup>(4, "FSD", "Faisalabad", 4),
            Row<CityLookup>(5, "RWP", "Rawalpindi", 5),
            Row<CityLookup>(6, "MUX", "Multan", 6),
            Row<CityLookup>(7, "SKT", "Sialkot", 7));

        modelBuilder.Entity<VendorSpecialisationLookup>().HasData(
            Row<VendorSpecialisationLookup>(1, "KNITS", "Knits", 1),
            Row<VendorSpecialisationLookup>(2, "WOVENS", "Wovens", 2),
            Row<VendorSpecialisationLookup>(3, "UNIFORMS", "Uniforms", 3));

        modelBuilder.Entity<CategoryLookup>().HasData(
            new { Id = (short)1, Code = "TOPS", Name = "Tops", Description = (string?)null, SortSeq = (short)1, IsActive = true, ParentCategoryId = (short?)null },
            new { Id = (short)2, Code = "BOTTOMS", Name = "Bottoms", Description = (string?)null, SortSeq = (short)2, IsActive = true, ParentCategoryId = (short?)null },
            new { Id = (short)3, Code = "DRESSES", Name = "Dresses", Description = (string?)null, SortSeq = (short)3, IsActive = true, ParentCategoryId = (short?)null },
            new { Id = (short)4, Code = "OUTERWEAR", Name = "Outerwear", Description = (string?)null, SortSeq = (short)4, IsActive = true, ParentCategoryId = (short?)null },
            new { Id = (short)5, Code = "SLEEPWEAR", Name = "Sleepwear", Description = (string?)null, SortSeq = (short)5, IsActive = true, ParentCategoryId = (short?)null },
            new { Id = (short)6, Code = "ACCESSORIES", Name = "Accessories", Description = (string?)null, SortSeq = (short)6, IsActive = true, ParentCategoryId = (short?)null });

        modelBuilder.Entity<PaymentTermLookup>().HasData(
            new { Id = (short)1, Code = "ADV100", Name = "100% Advance", Description = (string?)null, SortSeq = (short)1, IsActive = true, DefaultAdvancePercent = 100m },
            new { Id = (short)2, Code = "NET50_50", Name = "50% Advance / 50% on Delivery", Description = (string?)null, SortSeq = (short)2, IsActive = true, DefaultAdvancePercent = 50m },
            new { Id = (short)3, Code = "NET30_70", Name = "30% Advance / 70% on Delivery", Description = (string?)null, SortSeq = (short)3, IsActive = true, DefaultAdvancePercent = 30m },
            new { Id = (short)4, Code = "NET30", Name = "Net 30 (no advance)", Description = (string?)null, SortSeq = (short)4, IsActive = true, DefaultAdvancePercent = 0m });

        // System-owned (spec AC-1): only the states this sprint's PO state machine actually
        // reaches (Draft -> SentToVendor -> Acknowledged, or Cancelled). Later sprints add rows
        // for InProduction/PartiallyDelivered/Delivered/Closed as those transitions are built.
        modelBuilder.Entity<PoStatusLookup>().HasData(
            Row<PoStatusLookup>(1, "Draft", "Draft", 1),
            Row<PoStatusLookup>(2, "SentToVendor", "Sent to Vendor", 2),
            Row<PoStatusLookup>(3, "Acknowledged", "Acknowledged", 3),
            Row<PoStatusLookup>(4, "Cancelled", "Cancelled", 4));

        // Spec Decisions: exact seed list given by the spec itself.
        modelBuilder.Entity<PoCancelReasonLookup>().HasData(
            Row<PoCancelReasonLookup>(1, "VendorDeclined", "Vendor Declined", 1),
            Row<PoCancelReasonLookup>(2, "CostDispute", "Cost Dispute", 2),
            Row<PoCancelReasonLookup>(3, "QualityConcern", "Quality Concern", 3),
            Row<PoCancelReasonLookup>(4, "StyleDiscontinued", "Style Discontinued", 4),
            Row<PoCancelReasonLookup>(5, "DuplicateEntry", "Duplicate Entry", 5),
            Row<PoCancelReasonLookup>(6, "Other", "Other", 6));
    }

    /// <summary>The standard lookup shape's HasData row, for the nine types with no extra column.</summary>
    private static object Row<TLookup>(short id, string code, string name, short sortSeq)
        where TLookup : Lookup =>
        new { Id = id, Code = code, Name = name, Description = (string?)null, SortSeq = sortSeq, IsActive = true };
}
