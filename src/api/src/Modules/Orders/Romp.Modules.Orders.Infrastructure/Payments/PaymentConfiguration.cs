using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Infrastructure.Payments;

/// <summary>ORDR.PAYM_MAIN: the payment of one order. One row per order; the amount is the server-priced total (SEC-09).</summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("PAYM_MAIN", table => table.HasCheckConstraint("CK_PAYM_MAIN_AMT", "\"AMT\" > 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(p => p.MethodId).HasColumnName("PAYM_METH_ID").IsRequired();
        builder.Property(p => p.StatusId).HasColumnName("PAYM_STS_ID").IsRequired();
        builder.Property(p => p.Amount).HasColumnName("AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.WindowEndsAt).HasColumnName("WNDW_END_DTE");
        builder.Property(p => p.PaidAt).HasColumnName("PAID_DTE");
        builder.Property(p => p.ReviewReasonId).HasColumnName("RVW_RSN_ID");
        builder.Ignore(p => p.AttemptCount);
        builder.Ignore(p => p.OpenAttempt);

        builder.HasIndex(p => p.OrderId).IsUnique().HasDatabaseName("UQ_PAYM_MAIN_ORDR_ID");
        builder.HasIndex(p => new { p.StatusId, p.WindowEndsAt }).HasDatabaseName("IX_PAYM_MAIN_PAYM_STS_ID_WNDW_END_DTE");
        builder.HasIndex(p => p.MethodId).HasDatabaseName("IX_PAYM_MAIN_PAYM_METH_ID");
        builder.HasIndex(p => p.ReviewReasonId).HasDatabaseName("IX_PAYM_MAIN_RVW_RSN_ID");
        builder.HasAuditColumns();

        builder.HasOne<Order>().WithOne().HasForeignKey<Payment>(p => p.OrderId).HasConstraintName("FK_PAYM_MAIN_ORDR_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMethodLookup>().WithMany().HasForeignKey(p => p.MethodId).HasConstraintName("FK_PAYM_MAIN_PAYM_METH_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentStatusLookup>().WithMany().HasForeignKey(p => p.StatusId).HasConstraintName("FK_PAYM_MAIN_PAYM_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentReviewReasonLookup>().WithMany().HasForeignKey(p => p.ReviewReasonId).HasConstraintName("FK_PAYM_MAIN_PAYM_RVW_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.Metadata.FindNavigation(nameof(Payment.Attempts))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>ORDR.PAYM_ATMP: one try at paying. The gateway reference is unique, so a late message always finds its attempt.</summary>
internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PAYM_ATMP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.PaymentId).HasColumnName("PAYM_ID").IsRequired();
        builder.Property(a => a.AttemptNo).HasColumnName("ATMP_NO").IsRequired();
        builder.Property(a => a.GatewayRef).HasColumnName("PRVD_REF").HasMaxLength(PaymentAttempt.MaxReferenceLength).IsRequired();
        builder.Property(a => a.RedirectUrl).HasColumnName("URL").HasMaxLength(PaymentAttempt.MaxUrlLength);
        builder.Property(a => a.StatusId).HasColumnName("PAYM_STS_ID").IsRequired();
        builder.Property(a => a.StartedAt).HasColumnName("STRT_DTE").IsRequired();
        builder.Property(a => a.SettledAt).HasColumnName("END_DTE");

        // One attempt number once per payment: this is what stops two racing starts both saving a session.
        builder.HasIndex(a => new { a.PaymentId, a.AttemptNo }).IsUnique().HasDatabaseName("UQ_PAYM_ATMP_PAYM_ID_ATMP_NO");
        builder.HasIndex(a => a.GatewayRef).IsUnique().HasDatabaseName("UQ_PAYM_ATMP_PRVD_REF");
        builder.HasIndex(a => new { a.StatusId, a.StartedAt }).HasDatabaseName("IX_PAYM_ATMP_PAYM_STS_ID_STRT_DTE");
        builder.HasAuditColumns();

        builder.HasOne<Payment>().WithMany(p => p.Attempts).HasForeignKey(a => a.PaymentId).HasConstraintName("FK_PAYM_ATMP_PAYM_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentStatusLookup>().WithMany().HasForeignKey(a => a.StatusId).HasConstraintName("FK_PAYM_ATMP_PAYM_STS_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>ORDR.PAYM_WBHK: every message received on the webhook, signed or not. Only a hash of the body is kept, never the payload (PAY-9).</summary>
internal sealed class PaymentWebhookConfiguration : IEntityTypeConfiguration<PaymentWebhook>
{
    public void Configure(EntityTypeBuilder<PaymentWebhook> builder)
    {
        builder.ToTable("PAYM_WBHK");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(w => w.Provider).HasColumnName("PRVD").HasMaxLength(30).IsRequired();
        builder.Property(w => w.EventId).HasColumnName("EVNT_ID").HasMaxLength(100);
        builder.Property(w => w.SignatureVerified).HasColumnName("SGNT_VRFD_IND").IsRequired();
        builder.Property(w => w.BodyHash).HasColumnName("HASH").HasMaxLength(PaymentWebhook.HashLength).IsRequired();
        builder.Property(w => w.OutcomeId).HasColumnName("WBHK_OTCM_ID").IsRequired();
        builder.Property(w => w.GatewayRef).HasColumnName("PRVD_REF").HasMaxLength(PaymentAttempt.MaxReferenceLength);
        builder.Property(w => w.PaymentId).HasColumnName("PAYM_ID");
        builder.Property(w => w.ReceivedAt).HasColumnName("RCVD_DTE").IsRequired();

        // The provider's event id is unique per provider: a repeated delivery cannot be stored twice, even in a race (PAY-5).
        builder.HasIndex(w => new { w.Provider, w.EventId }).IsUnique().HasDatabaseName("UQ_PAYM_WBHK_PRVD_EVNT_ID").HasFilter("\"EVNT_ID\" IS NOT NULL");
        builder.HasIndex(w => w.PaymentId).HasDatabaseName("IX_PAYM_WBHK_PAYM_ID");
        builder.HasIndex(w => w.OutcomeId).HasDatabaseName("IX_PAYM_WBHK_WBHK_OTCM_ID");
        builder.HasIndex(w => w.ReceivedAt).HasDatabaseName("IX_PAYM_WBHK_RCVD_DTE");
        builder.HasAuditColumns();

        builder.HasOne<Payment>().WithMany().HasForeignKey(w => w.PaymentId).HasConstraintName("FK_PAYM_WBHK_PAYM_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WebhookOutcomeLookup>().WithMany().HasForeignKey(w => w.OutcomeId).HasConstraintName("FK_PAYM_WBHK_WBHK_OTCM_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}
