using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Mfa;

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("USR_RCVR_CODE");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.UserId).HasColumnName("USR_ID").IsRequired();
        builder.Property(c => c.CodeHash).HasColumnName("CODE_HASH").HasMaxLength(32).IsRequired();
        builder.Property(c => c.UsedAt).HasColumnName("USED_DTE");
        builder.HasIndex(c => c.UserId);

        builder.HasOne<StaffUser>().WithMany().HasForeignKey(c => c.UserId)
            .HasConstraintName("FK_USR_RCVR_CODE_USR_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}

internal sealed class MfaEnrolmentPendingConfiguration : IEntityTypeConfiguration<MfaEnrolmentPending>
{
    public void Configure(EntityTypeBuilder<MfaEnrolmentPending> builder)
    {
        builder.ToTable("MFA_ENRL_PEND");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.UserId).HasColumnName("USR_ID").IsRequired();
        builder.Property(p => p.SecretCipher).HasColumnName("SECR_ENC").HasMaxLength(128).IsRequired();
        builder.Property(p => p.KeyId).HasColumnName("KEY_ID").HasMaxLength(40).IsRequired();
        builder.Property(p => p.ExpiresAt).HasColumnName("EXPY_DTE").IsRequired();

        // One unconfirmed enrolment per account: starting again replaces it.
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.HasOne<StaffUser>().WithMany().HasForeignKey(p => p.UserId)
            .HasConstraintName("FK_MFA_ENRL_PEND_USR_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}
