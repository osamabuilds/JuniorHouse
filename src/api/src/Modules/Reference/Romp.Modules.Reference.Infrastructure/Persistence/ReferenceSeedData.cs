using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Reference.Domain;
using Romp.Modules.Reference.Domain.Lookups;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;
using Romp.Modules.Reference.Domain.Lookups.Vendors;

namespace Romp.Modules.Reference.Infrastructure.Persistence;

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

        // Sprint 2 (SCRUM-93). Staff-maintained (plan.md Data section, exact seed list from the
        // spec's Non-functional constraints).
        modelBuilder.Entity<AmendmentReasonLookup>().HasData(
            Row<AmendmentReasonLookup>(1, "VendorCostIncrease", "Vendor Cost Increase", 1),
            Row<AmendmentReasonLookup>(2, "MoqConstraint", "MOQ Constraint", 2),
            Row<AmendmentReasonLookup>(3, "FabricOrTrimUnavailable", "Fabric or Trim Unavailable", 3),
            Row<AmendmentReasonLookup>(4, "CapacityDelay", "Capacity Delay", 4),
            Row<AmendmentReasonLookup>(5, "AdvanceRequest", "Advance Request", 5),
            Row<AmendmentReasonLookup>(6, "SizeMixChange", "Size Mix Change", 6),
            Row<AmendmentReasonLookup>(7, "ColourChange", "Colour Change", 7),
            Row<AmendmentReasonLookup>(8, "SpecChange", "Spec Change", 8),
            Row<AmendmentReasonLookup>(9, "SafetyOrCompliance", "Safety or Compliance", 9),
            Row<AmendmentReasonLookup>(10, "BuyerDemandChange", "Buyer Demand Change", 10),
            Row<AmendmentReasonLookup>(11, "Other", "Other", 11));

        modelBuilder.Entity<VendorCommChannelLookup>().HasData(
            Row<VendorCommChannelLookup>(1, "WhatsApp", "WhatsApp", 1),
            Row<VendorCommChannelLookup>(2, "PhoneCall", "Phone Call", 2),
            Row<VendorCommChannelLookup>(3, "Email", "Email", 3),
            Row<VendorCommChannelLookup>(4, "InPerson", "In Person", 4),
            Row<VendorCommChannelLookup>(5, "Unspecified", "Unspecified", 5));

        modelBuilder.Entity<FabricResponsibilityLookup>().HasData(
            Row<FabricResponsibilityLookup>(1, "VendorSupplied", "Vendor Supplied", 1),
            Row<FabricResponsibilityLookup>(2, "RompSupplied", "Romp Supplied", 2));

        // Sprint 2, system-owned (spec section D / ADR 0007): the PO revision state machine.
        modelBuilder.Entity<RevisionStatusLookup>().HasData(
            Row<RevisionStatusLookup>(1, "Pending", "Pending", 1),
            Row<RevisionStatusLookup>(2, "InForce", "In Force", 2),
            Row<RevisionStatusLookup>(3, "Superseded", "Superseded", 3),
            Row<RevisionStatusLookup>(4, "Rejected", "Rejected", 4),
            Row<RevisionStatusLookup>(5, "Withdrawn", "Withdrawn", 5));

        modelBuilder.Entity<AmendmentInitiatorLookup>().HasData(
            Row<AmendmentInitiatorLookup>(1, "Buyer", "Buyer", 1),
            Row<AmendmentInitiatorLookup>(2, "Vendor", "Vendor", 2));

        modelBuilder.Entity<PoVendorCommTypeLookup>().HasData(
            Row<PoVendorCommTypeLookup>(1, "Confirmed", "Confirmed", 1),
            Row<PoVendorCommTypeLookup>(2, "Countered", "Countered", 2),
            Row<PoVendorCommTypeLookup>(3, "Declined", "Declined", 3),
            Row<PoVendorCommTypeLookup>(4, "AmendmentRequest", "Amendment Request", 4),
            Row<PoVendorCommTypeLookup>(5, "Decision", "Decision", 5));

        // Sprint 2, system-owned (spec section F): the vendor-visible/internal split is structural,
        // not staff-editable.
        modelBuilder.Entity<PoFileCategoryLookup>().HasData(
            new { Id = (short)1, Code = "TechPackSpec", Name = "Tech Pack Spec", Description = (string?)null, SortSeq = (short)1, IsActive = true, IsVendorVisible = true },
            new { Id = (short)2, Code = "ArtworkLabels", Name = "Artwork / Labels", Description = (string?)null, SortSeq = (short)2, IsActive = true, IsVendorVisible = true },
            new { Id = (short)3, Code = "TrimCardBom", Name = "Trim Card / BOM", Description = (string?)null, SortSeq = (short)3, IsActive = true, IsVendorVisible = true },
            new { Id = (short)4, Code = "ColourStandard", Name = "Colour Standard", Description = (string?)null, SortSeq = (short)4, IsActive = true, IsVendorVisible = true },
            new { Id = (short)5, Code = "PackingInstructions", Name = "Packing Instructions", Description = (string?)null, SortSeq = (short)5, IsActive = true, IsVendorVisible = true },
            new { Id = (short)6, Code = "CostSheet", Name = "Cost Sheet", Description = (string?)null, SortSeq = (short)6, IsActive = true, IsVendorVisible = false },
            new { Id = (short)7, Code = "ComplianceTestReport", Name = "Compliance / Test Report", Description = (string?)null, SortSeq = (short)7, IsActive = true, IsVendorVisible = false },
            new { Id = (short)8, Code = "VendorEvidence", Name = "Vendor Evidence", Description = (string?)null, SortSeq = (short)8, IsActive = true, IsVendorVisible = false },
            new { Id = (short)9, Code = "Other", Name = "Other", Description = (string?)null, SortSeq = (short)9, IsActive = true, IsVendorVisible = false });
    }

    /// <summary>The standard lookup shape's HasData row, for the nine types with no extra column.</summary>
    private static object Row<TLookup>(short id, string code, string name, short sortSeq)
        where TLookup : Lookup =>
        new { Id = id, Code = code, Name = name, Description = (string?)null, SortSeq = sortSeq, IsActive = true };
}
