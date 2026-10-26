using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.StaffAccounts;

internal sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.ToTable("USR_MAIN", table =>
        {
            // The name is stored lower-case so a plain unique index is case-insensitive (AC-10).
            table.HasCheckConstraint("CK_USR_MAIN_USR_NAME", "\"USR_NAME\" ~ '^[a-z0-9._-]{3,40}$'");
        });
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(u => u.UserName).HasColumnName("USR_NAME").HasMaxLength(UserName.MaxLength).IsRequired();
        builder.HasIndex(u => u.UserName).IsUnique();
        builder.Property(u => u.DisplayName).HasColumnName("DISP_NAME").HasMaxLength(StaffUser.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("PWD_HASH").HasMaxLength(255).IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.Property(u => u.MustChangePassword).HasColumnName("MUST_CHNG_PWD_IND").IsRequired();
        builder.Property(u => u.TemporaryPasswordExpiresAt).HasColumnName("TEMP_PWD_EXPY_DTE");
        builder.Property(u => u.TemporaryPasswordUsedAt).HasColumnName("TEMP_PWD_USED_DTE");
        builder.Property(u => u.MfaSecretCipher).HasColumnName("MFA_SECR_ENC").HasMaxLength(128);
        builder.Property(u => u.MfaKeyId).HasColumnName("MFA_KEY_ID").HasMaxLength(40);
        builder.Property(u => u.MfaEnrolledAt).HasColumnName("MFA_ENRL_DTE");
        builder.Property(u => u.MfaLastStep).HasColumnName("MFA_LAST_STEP").IsRequired();
        builder.Ignore(u => u.IsMfaEnrolled);
        builder.Ignore(u => u.HasTemporaryPassword);
        builder.Ignore(u => u.RoleIds);
        builder.Ignore(u => u.DomainEvents);

        builder.HasMany(u => u.Roles).WithOne().HasForeignKey(r => r.UserId)
            .HasConstraintName("FK_USR_ROLE_MAP_USR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(u => u.Roles).HasField("_roles").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.PasswordHistory).WithOne().HasForeignKey(h => h.UserId)
            .HasConstraintName("FK_USR_PWD_HIST_USR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(u => u.PasswordHistory).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasAuditColumns();
    }
}

internal sealed class StaffUserRoleConfiguration : IEntityTypeConfiguration<StaffUserRole>
{
    public void Configure(EntityTypeBuilder<StaffUserRole> builder)
    {
        builder.ToTable("USR_ROLE_MAP");
        builder.HasKey(r => new { r.UserId, r.RoleId });
        builder.Property(r => r.UserId).HasColumnName("USR_ID");
        builder.Property(r => r.RoleId).HasColumnName("ROLE_ID");
        builder.HasOne<Role>().WithMany().HasForeignKey(r => r.RoleId)
            .HasConstraintName("FK_USR_ROLE_MAP_ROLE_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.RoleId); // the user side is covered by the primary key
    }
}

internal sealed class PasswordHistoryEntryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        builder.ToTable("USR_PWD_HIST");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.UserId).HasColumnName("USR_ID").IsRequired();
        builder.Property(h => h.PasswordHash).HasColumnName("PWD_HASH").HasMaxLength(255).IsRequired();
        builder.Property(h => h.RetiredAt).HasColumnName("RETD_DTE").IsRequired();
        builder.HasIndex(h => h.UserId);
        builder.HasAuditColumns();
    }
}
