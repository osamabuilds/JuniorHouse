using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Application.Allocations;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain.Allocations;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.Allocations;

internal sealed class StockAllocationConfiguration : IEntityTypeConfiguration<StockAllocation>
{
    public void Configure(EntityTypeBuilder<StockAllocation> builder)
    {
        builder.ToTable("STCK_ALOC");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.OrderKey).HasColumnName("ORDR_KEY").HasMaxLength(StockAllocation.MaxKeyLength).IsRequired();
        builder.Property(a => a.StatusId).HasColumnName("ALOC_STS_ID").IsRequired();
        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.IsActive);

        // One allocation per order key: asking twice is the same allocation (ALC-5).
        builder.HasIndex(a => a.OrderKey).IsUnique().HasDatabaseName("UQ_STCK_ALOC_ORDR_KEY");
        builder.HasIndex(a => a.StatusId).HasDatabaseName("IX_STCK_ALOC_ALOC_STS_ID");
        builder.HasAuditColumns();

        builder.HasOne<AllocationStatusLookup>().WithMany().HasForeignKey(a => a.StatusId).HasConstraintName("FK_STCK_ALOC_ALOC_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.Metadata.FindNavigation(nameof(StockAllocation.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AllocationLineConfiguration : IEntityTypeConfiguration<AllocationLine>
{
    public void Configure(EntityTypeBuilder<AllocationLine> builder)
    {
        builder.ToTable("STCK_ALOC_LINE", table => table.HasCheckConstraint("CK_STCK_ALOC_LINE_QTY", "\"QTY\" > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.AllocationId).HasColumnName("ALOC_ID").IsRequired();
        builder.Property(l => l.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(l => l.GradeId).HasColumnName("GRD_ID").IsRequired();
        builder.Property(l => l.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(l => l.Quantity).HasColumnName("QTY").IsRequired();

        builder.HasIndex(l => new { l.AllocationId, l.VariantRefId, l.GradeId, l.BinId }).IsUnique().HasDatabaseName("UQ_STCK_ALOC_LINE_ALOC_VRNT_GRD_BIN");
        // The free-units query reads claims by variant, grade and bin.
        builder.HasIndex(l => new { l.VariantRefId, l.GradeId, l.BinId }).HasDatabaseName("IX_STCK_ALOC_LINE_VRNT_REF_ID_GRD_ID_BIN_ID");
        builder.HasIndex(l => l.BinId).HasDatabaseName("IX_STCK_ALOC_LINE_BIN_ID");
        builder.HasAuditColumns();

        builder.HasOne<StockAllocation>().WithMany(a => a.Lines).HasForeignKey(l => l.AllocationId).HasConstraintName("FK_STCK_ALOC_LINE_STCK_ALOC").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<VariantRef>().WithMany().HasForeignKey(l => l.VariantRefId).HasConstraintName("FK_STCK_ALOC_LINE_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bin>().WithMany().HasForeignKey(l => l.BinId).HasConstraintName("FK_STCK_ALOC_LINE_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Raw SQL for allocation: the variant row locks (same lock the reservation ledger and the posting port take, in ascending id order) and
/// the free units per bin with the FIFO date. Every identifier is quoted.
/// </summary>
public sealed class AllocationCandidates(WhseDbContext dbContext) : IAllocationCandidates
{
    private const string LockSql = """SELECT "ID" FROM "WHSE"."VRNT_REF" WHERE "ID" = {0} FOR UPDATE""";

    // Free = balance minus units claimed by active allocations. Since = the oldest putaway after the balance last hit zero (Q-ALC-1).
    private const string FindSql =
        """
        SELECT b."BIN_ID" AS "BinId", b."GRD_ID" AS "GradeId", n."BIN_CODE" AS "BinCode", n."ZONE" AS "Zone", n."AISL" AS "Aisle", n."SHLF" AS "Shelf",
            (b."QTY" - COALESCE((
                SELECT SUM(l."QTY") FROM "WHSE"."STCK_ALOC_LINE" l
                JOIN "WHSE"."STCK_ALOC" a ON a."ID" = l."ALOC_ID"
                WHERE a."ALOC_STS_ID" = 1 AND l."VRNT_REF_ID" = b."VRNT_REF_ID" AND l."GRD_ID" = b."GRD_ID" AND l."BIN_ID" = b."BIN_ID"), 0))::int AS "Free",
            COALESCE((
                SELECT MIN(m."INSR_DTE") FROM "WHSE"."STCK_MVMT" m
                WHERE m."VRNT_REF_ID" = b."VRNT_REF_ID" AND m."GRD_ID" = b."GRD_ID" AND m."BIN_ID" = b."BIN_ID" AND m."QTY_CHNG" > 0
                  AND m."ID" > COALESCE((
                        SELECT MAX(z."ID") FROM "WHSE"."STCK_MVMT" z
                        WHERE z."VRNT_REF_ID" = b."VRNT_REF_ID" AND z."GRD_ID" = b."GRD_ID" AND z."BIN_ID" = b."BIN_ID" AND z."AFT_QTY" = 0), 0)),
                b."INSR_DTE") AS "Since"
        FROM "WHSE"."STCK_BAL" b
        JOIN "WHSE"."BIN_MAIN" n ON n."ID" = b."BIN_ID"
        WHERE b."VRNT_REF_ID" = {0} AND b."QTY" > 0 AND n."ACT_IND" AND n."RVW_RSN" IS NULL
        """;

    public async Task LockVariantsAsync(IReadOnlyList<long> variantRefIds, CancellationToken cancellationToken)
    {
        foreach (var id in variantRefIds.Distinct().Order())
        {
            await dbContext.Database.ExecuteSqlRawAsync(LockSql, [id], cancellationToken);
        }
    }

    public async Task<IReadOnlyList<BinCandidate>> FindAsync(long variantRefId, CancellationToken cancellationToken) =>
        await dbContext.Database.SqlQueryRaw<BinCandidate>(FindSql, variantRefId).ToListAsync(cancellationToken);
}

/// <summary>The Warehouse side of <see cref="IStockAllocator"/>: each call is one command, so it runs in its own Warehouse transaction (ADR 0030).</summary>
public sealed class StockAllocator(ISender sender) : IStockAllocator
{
    public Task<AllocationResult> AllocateAsync(string orderKey, IReadOnlyList<AllocationLineRequest> lines, CancellationToken cancellationToken) =>
        sender.Send(new AllocateStockCommand(orderKey, lines), cancellationToken);

    public async Task ReleaseAsync(string orderKey, CancellationToken cancellationToken) =>
        await sender.Send(new ReleaseAllocationCommand(orderKey), cancellationToken);

    public async Task ConfirmPickAsync(string orderKey, IReadOnlyList<PickedUnit> picks, string staff, CancellationToken cancellationToken) =>
        await sender.Send(new ConfirmPickCommand(orderKey, picks, staff), cancellationToken);

    public async Task FlagBinAsync(long binId, string reasonCode, CancellationToken cancellationToken) =>
        await sender.Send(new SetBinReviewCommand(binId, reasonCode), cancellationToken);

    public async Task ClearBinFlagAsync(long binId, CancellationToken cancellationToken) =>
        await sender.Send(new SetBinReviewCommand(binId, null), cancellationToken);
}
