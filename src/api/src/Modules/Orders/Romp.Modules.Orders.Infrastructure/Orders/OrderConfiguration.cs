using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Infrastructure.Orders;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("ORDR_MAIN", table =>
        {
            table.HasCheckConstraint("CK_ORDR_MAIN_AMTS", "\"SUBT_AMT\" > 0 AND \"DISC_AMT\" >= 0 AND \"SHPG_AMT\" >= 0 AND \"DISC_AMT\" <= \"SUBT_AMT\" AND \"TOT_AMT\" = \"SUBT_AMT\" - \"DISC_AMT\" + \"SHPG_AMT\"");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(o => o.OrderNumber).HasColumnName("ORDR_NO").HasMaxLength(OrderNumbers.MaxLength).IsRequired();
        builder.Property(o => o.PlacementKey).HasColumnName("PLCE_KEY").HasMaxLength(64).IsRequired();
        builder.Property(o => o.CustomerId).HasColumnName("CUST_ID");
        builder.Property(o => o.StatusId).HasColumnName("ORDR_STS_ID").IsRequired();
        builder.Property(o => o.PaymentMethodId).HasColumnName("PAYM_METH_ID").IsRequired();
        builder.Property(o => o.CartRef).HasColumnName("CART_REF").HasMaxLength(40);
        builder.Property(o => o.ContactName).HasColumnName("CNTC_NAME").HasMaxLength(100).IsRequired();
        builder.Property(o => o.ContactEmail).HasColumnName("CNTC_EML").HasMaxLength(254);
        builder.Property(o => o.ContactMobile).HasColumnName("CNTC_PHON").HasMaxLength(20).IsRequired();
        builder.Property(o => o.WhatsAppOptIn).HasColumnName("WHTS_OPTN_IND").IsRequired();
        builder.Property(o => o.WhatsAppOptInAt).HasColumnName("WHTS_OPTN_DTE");
        builder.Property(o => o.RecipientName).HasColumnName("RCPT_NAME").HasMaxLength(100).IsRequired();
        builder.Property(o => o.RecipientPhone).HasColumnName("RCPT_PHON").HasMaxLength(20).IsRequired();
        builder.Property(o => o.AddressLine1).HasColumnName("LINE_ONE").HasMaxLength(200).IsRequired();
        builder.Property(o => o.AddressLine2).HasColumnName("LINE_TWO").HasMaxLength(200);
        builder.Property(o => o.CityId).HasColumnName("CITY_ID").IsRequired();
        builder.Property(o => o.CityName).HasColumnName("CITY_NAME").HasMaxLength(100).IsRequired();
        builder.Property(o => o.ProvinceId).HasColumnName("PROV_ID").IsRequired();
        builder.Property(o => o.ProvinceName).HasColumnName("PROV_NAME").HasMaxLength(100).IsRequired();
        builder.Property(o => o.PostalCode).HasColumnName("PCDE").HasMaxLength(10);
        builder.Property(o => o.DeliveryNote).HasColumnName("DLVR_NOTE").HasMaxLength(200);
        builder.Property(o => o.Subtotal).HasColumnName("SUBT_AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(o => o.Discount).HasColumnName("DISC_AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(o => o.Shipping).HasColumnName("SHPG_AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(o => o.Total).HasColumnName("TOT_AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(o => o.DiscountCode).HasColumnName("DISC_CODE").HasMaxLength(32);
        builder.Property(o => o.PlacedAt).HasColumnName("PLCD_DTE").IsRequired();
        builder.Property(o => o.CodNextDueAt).HasColumnName("COD_DUE_DTE");
        builder.Property(o => o.CodAutoCancelAt).HasColumnName("COD_CNCL_DTE");
        builder.Property(o => o.CodAttemptCount).HasColumnName("COD_ATMP_CNT").IsRequired();
        builder.Property(o => o.CodWhatsAppAskedAt).HasColumnName("COD_WHTS_DTE");
        builder.Property(o => o.IsFirstOrder).HasColumnName("FRST_ORDR_IND").IsRequired();
        builder.Property(o => o.IsHighValue).HasColumnName("HI_VAL_IND").IsRequired();
        builder.Property(o => o.IsRefusalFlagged).HasColumnName("RFSL_FLG_IND").IsRequired();
        builder.Ignore(o => o.DomainEvents);

        builder.HasIndex(o => o.OrderNumber).IsUnique().HasDatabaseName("UQ_ORDR_MAIN_ORDR_NO");
        builder.HasIndex(o => o.PlacementKey).IsUnique().HasDatabaseName("UQ_ORDR_MAIN_PLCE_KEY");
        builder.HasIndex(o => new { o.StatusId, o.PlacedAt }).HasDatabaseName("IX_ORDR_MAIN_ORDR_STS_ID_PLCD_DTE");
        builder.HasIndex(o => o.CustomerId).HasDatabaseName("IX_ORDR_MAIN_CUST_ID");
        builder.HasIndex(o => o.ContactMobile).HasDatabaseName("IX_ORDR_MAIN_CNTC_PHON");
        builder.HasIndex(o => o.PaymentMethodId).HasDatabaseName("IX_ORDR_MAIN_PAYM_METH_ID");
        // The staff "due now" list and the jobs look for pending orders by due time.
        builder.HasIndex(o => o.CodNextDueAt).HasDatabaseName("IX_ORDR_MAIN_COD_DUE_DTE").HasFilter("\"COD_DUE_DTE\" IS NOT NULL");
        builder.HasIndex(o => o.CodAutoCancelAt).HasDatabaseName("IX_ORDR_MAIN_COD_CNCL_DTE").HasFilter("\"COD_CNCL_DTE\" IS NOT NULL");
        builder.HasAuditColumns();

        builder.HasOne<OrderStatusLookup>().WithMany().HasForeignKey(o => o.StatusId).HasConstraintName("FK_ORDR_MAIN_ORDR_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMethodLookup>().WithMany().HasForeignKey(o => o.PaymentMethodId).HasConstraintName("FK_ORDR_MAIN_PAYM_METH_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.Metadata.FindNavigation(nameof(Order.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Order.History))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Order.CodAttempts))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("ORDR_LINE", table => table.HasCheckConstraint("CK_ORDR_LINE_QTY_PRIC", "\"QTY\" > 0 AND \"UNIT_PRIC\" > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(l => l.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(l => l.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(l => l.Sku).HasColumnName("SKU").HasMaxLength(40);
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(200).IsRequired();
        builder.Property(l => l.Colour).HasColumnName("CLR_NAME").HasMaxLength(60).IsRequired();
        builder.Property(l => l.Size).HasColumnName("SIZE_NAME").HasMaxLength(40).IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnName("UNIT_PRIC").HasPrecision(12, 2).IsRequired();
        builder.Property(l => l.Quantity).HasColumnName("QTY").IsRequired();
        builder.Ignore(l => l.LineTotal);

        // One line per variant per order: the basket merges equal variants before it gets here.
        builder.HasIndex(l => new { l.OrderId, l.VariantId }).IsUnique().HasDatabaseName("UQ_ORDR_LINE_ORDR_ID_VRNT_ID");
        builder.HasIndex(l => l.VariantId).HasDatabaseName("IX_ORDR_LINE_VRNT_ID");
        builder.HasAuditColumns();

        builder.HasOne<Order>().WithMany(o => o.Lines).HasForeignKey(l => l.OrderId).HasConstraintName("FK_ORDR_LINE_ORDR_MAIN").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderHistoryConfiguration : IEntityTypeConfiguration<OrderHistory>
{
    public void Configure(EntityTypeBuilder<OrderHistory> builder)
    {
        builder.ToTable("ORDR_HIST");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(h => h.FromStatusId).HasColumnName("FROM_STS_ID");
        builder.Property(h => h.ToStatusId).HasColumnName("TO_STS_ID").IsRequired();
        builder.Property(h => h.ReasonId).HasColumnName("CNCL_RSN_ID");
        builder.Property(h => h.ActorKindId).HasColumnName("ACTR_KIND_ID").IsRequired();
        builder.Property(h => h.ActorRef).HasColumnName("ACTR_REF").HasMaxLength(64);
        builder.Property(h => h.Note).HasColumnName("NOTE").HasMaxLength(OrderHistory.MaxNoteLength);
        builder.Property(h => h.At).HasColumnName("OCCR_DTE").IsRequired();

        builder.HasIndex(h => new { h.OrderId, h.Id }).HasDatabaseName("IX_ORDR_HIST_ORDR_ID_ID");
        builder.HasIndex(h => h.ToStatusId).HasDatabaseName("IX_ORDR_HIST_TO_STS_ID");
        builder.HasIndex(h => h.FromStatusId).HasDatabaseName("IX_ORDR_HIST_FROM_STS_ID");
        builder.HasIndex(h => h.ReasonId).HasDatabaseName("IX_ORDR_HIST_CNCL_RSN_ID");
        builder.HasIndex(h => h.ActorKindId).HasDatabaseName("IX_ORDR_HIST_ACTR_KIND_ID");
        builder.HasAuditColumns();

        builder.HasOne<Order>().WithMany(o => o.History).HasForeignKey(h => h.OrderId).HasConstraintName("FK_ORDR_HIST_ORDR_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderStatusLookup>().WithMany().HasForeignKey(h => h.ToStatusId).HasConstraintName("FK_ORDR_HIST_TO_STS").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderStatusLookup>().WithMany().HasForeignKey(h => h.FromStatusId).HasConstraintName("FK_ORDR_HIST_FROM_STS").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CancelReasonLookup>().WithMany().HasForeignKey(h => h.ReasonId).HasConstraintName("FK_ORDR_HIST_CNCL_RSN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ActorKindLookup>().WithMany().HasForeignKey(h => h.ActorKindId).HasConstraintName("FK_ORDR_HIST_ACTR_KIND").OnDelete(DeleteBehavior.Restrict);
    }
}
