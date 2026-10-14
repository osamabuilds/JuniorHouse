using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Warehouse.Application.StockAlerts;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockAlerts;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.StockAlerts;

internal sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("ALRT_MAIN");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(a => a.LevelId).HasColumnName("ALRT_LVL_ID").IsRequired();
        builder.Property(a => a.StatusId).HasColumnName("ALRT_STS_ID").IsRequired();
        builder.Property(a => a.LastQuantity).HasColumnName("LAST_QTY").IsRequired();
        builder.Property(a => a.Threshold).HasColumnName("THRS_QTY").IsRequired();
        builder.Property(a => a.OpenedAt).HasColumnName("OPEN_DTE").IsRequired();
        builder.Property(a => a.ClosedAt).HasColumnName("CLSD_DTE");
        builder.Property(a => a.AcknowledgedAt).HasColumnName("ACK_DTE");
        builder.Property(a => a.AcknowledgedBy).HasColumnName("ACK_BY").HasMaxLength(100);
        builder.Ignore(a => a.IsOpen);

        // ALR-3: at most ONE open alert per variant, enforced by the database and not only by the evaluator.
        builder.HasIndex(a => a.VariantRefId).IsUnique().HasFilter($"\"ALRT_STS_ID\" = {AlertStatuses.Open}").HasDatabaseName("UX_ALRT_MAIN_OPEN");
        builder.HasIndex(a => new { a.StatusId, a.Id });
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithMany().HasForeignKey(a => a.VariantRefId).HasConstraintName("FK_ALRT_MAIN_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AlertLevelLookup>().WithMany().HasForeignKey(a => a.LevelId).HasConstraintName("FK_ALRT_MAIN_ALRT_LVL_LKP");
        builder.HasOne<AlertStatusLookup>().WithMany().HasForeignKey(a => a.StatusId).HasConstraintName("FK_ALRT_MAIN_ALRT_STS_LKP");
    }
}

internal sealed class LowStockThresholdConfiguration : IEntityTypeConfiguration<LowStockThreshold>
{
    public void Configure(EntityTypeBuilder<LowStockThreshold> builder)
    {
        builder.ToTable("ALRT_THRS", table => table.HasCheckConstraint("CK_ALRT_THRS_QTY_RANGE", "\"THRS_QTY\" >= 0 AND \"THRS_QTY\" <= 100000"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(t => t.Quantity).HasColumnName("THRS_QTY").IsRequired();

        builder.HasIndex(t => t.VariantRefId).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithMany().HasForeignKey(t => t.VariantRefId).HasConstraintName("FK_ALRT_THRS_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Alerts consume the module's own <c>StockLevelChangedEvent</c> through the shared dispatcher (ADR 0012), in a separate transaction from
/// the stock change. A failure here is retried and dead-lettered by the dispatcher and can never roll back the stock change (ALR-9,
/// NFR-FT-08). The inbox row commits with the evaluation, so a redelivered event is a no-op.
/// </summary>
internal sealed class StockLevelChangedAlertConsumer(ISender sender, WhseDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<StockLevelChangedEvent>
{
    public async Task HandleAsync(StockLevelChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "EvaluateAlertOnStockLevelChanged", cancellationToken))
        {
            await sender.Send(new EvaluateStockAlertCommand(domainEvent.VariantId), cancellationToken);
        }
    }
}

public sealed record SetThresholdRequest(int Quantity, string ActingPerson);

public sealed record AcknowledgeAlertRequest(string ActingPerson);

// Reads in Staff; threshold edits and acknowledgement in the Inventory group (FR-60, enforced for real in S8, SEC-08).
internal static class StockAlertEndpoints
{
    private const long MaxBodyBytes = 4 * 1024;

    public static void MapStockAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reads = endpoints.MapSecuredGroup("/api/warehouse/alerts").WithTags("Warehouse");

        reads.MapGet("", async (
                short? levelId,
                bool? acknowledged,
                bool? includeClosed,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchAlertsQuery(levelId, acknowledged, includeClosed ?? false, page, pageSize), cancellationToken)));

        reads.MapGet("/open-count", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CountOpenAlertsQuery(), cancellationToken)));

        reads.MapGet("/thresholds", async (
                string? search,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchThresholdsQuery(search, page, pageSize), cancellationToken)));

        var writes = endpoints.MapSecuredGroup("/api/warehouse/alerts", Policies.Inventory).WithTags("Warehouse")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        writes.MapPost("/{id:long}/acknowledge", async (long id, AcknowledgeAlertRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AcknowledgeAlertCommand(id, body.ActingPerson), cancellationToken)));

        writes.MapPut("/thresholds/{variantRefId:long}", async (long variantRefId, SetThresholdRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetThresholdCommand(variantRefId, body.Quantity, body.ActingPerson), cancellationToken)));
    }
}
