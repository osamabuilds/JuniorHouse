using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

internal sealed class RevokeReasonConfiguration : IEntityTypeConfiguration<RevokeReasonRow>
{
    public void Configure(EntityTypeBuilder<RevokeReasonRow> builder)
    {
        builder.ToTable("SESS_RVKD_RSN_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(SessionSeed.RevokeReasons);
    }
}

internal sealed class LoginOutcomeConfiguration : IEntityTypeConfiguration<LoginOutcomeRow>
{
    public void Configure(EntityTypeBuilder<LoginOutcomeRow> builder)
    {
        builder.ToTable("LGIN_OTCM_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(SessionSeed.LoginOutcomes);
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("SESS_MAIN");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.UserId).HasColumnName("USR_ID").IsRequired();
        builder.Property(s => s.AccessTokenHash).HasColumnName("ACSS_TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(s => s.RefreshTokenHash).HasColumnName("RFSH_TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(s => s.PreviousRefreshTokenHash).HasColumnName("PREV_RFSH_TOKN_HASH").HasMaxLength(32);
        builder.Property(s => s.StartedAt).HasColumnName("STRT_DTE").IsRequired();
        builder.Property(s => s.AccessExpiresAt).HasColumnName("ACSS_EXPY_DTE").IsRequired();
        builder.Property(s => s.RefreshExpiresAt).HasColumnName("RFSH_EXPY_DTE").IsRequired();
        builder.Property(s => s.AbsoluteExpiresAt).HasColumnName("ABS_EXPY_DTE").IsRequired();
        builder.Property(s => s.RotatedAt).HasColumnName("ROTD_DTE");
        builder.Property(s => s.RevokedAt).HasColumnName("RVKD_DTE");
        builder.Property(s => s.RevokeReason).HasColumnName("RVKD_RSN_ID").HasConversion<short?>();
        builder.Property(s => s.ClientIp).HasColumnName("CLNT_IP_ADDR").HasMaxLength(Session.ClientIpMaxLength);
        builder.Property(s => s.UserAgent).HasColumnName("USR_AGNT").HasMaxLength(Session.UserAgentMaxLength);
        builder.Ignore(s => s.DomainEvents);

        // Lookup by hash must find exactly one session.
        builder.HasIndex(s => s.AccessTokenHash).IsUnique();
        builder.HasIndex(s => s.RefreshTokenHash).IsUnique();
        builder.HasIndex(s => s.PreviousRefreshTokenHash).IsUnique();
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.RevokeReason);
        builder.HasIndex(s => s.AbsoluteExpiresAt); // the daily purge

        builder.HasOne<StaffUser>().WithMany().HasForeignKey(s => s.UserId)
            .HasConstraintName("FK_SESS_MAIN_USR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RevokeReasonRow>().WithMany().HasForeignKey(s => s.RevokeReason)
            .HasConstraintName("FK_SESS_MAIN_RVKD_RSN_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}

internal sealed class PendingSignInConfiguration : IEntityTypeConfiguration<PendingSignIn>
{
    public void Configure(EntityTypeBuilder<PendingSignIn> builder)
    {
        builder.ToTable("LGIN_PEND");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.UserId).HasColumnName("USR_ID").IsRequired();
        builder.Property(p => p.TokenHash).HasColumnName("TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(p => p.ExpiresAt).HasColumnName("EXPY_DTE").IsRequired();
        builder.Property(p => p.FailureCount).HasColumnName("FAIL_CNT").IsRequired();

        builder.HasIndex(p => p.TokenHash).IsUnique();
        builder.HasIndex(p => p.UserId);

        builder.HasOne<StaffUser>().WithMany().HasForeignKey(p => p.UserId)
            .HasConstraintName("FK_LGIN_PEND_USR_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}

internal sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LGIN_ATMP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.AttemptName).HasColumnName("ATMP_NAME").HasMaxLength(LoginAttempt.NameMaxLength).IsRequired();
        builder.Property(a => a.ClientIp).HasColumnName("CLNT_IP_ADDR").HasMaxLength(Session.ClientIpMaxLength).IsRequired();
        builder.Property(a => a.Outcome).HasColumnName("OTCM_ID").HasConversion<short>().IsRequired();
        builder.Property(a => a.AttemptedAt).HasColumnName("ATMP_DTE").IsRequired();
        builder.Property(a => a.UserId).HasColumnName("USR_ID");

        // The throttle query (name + address, newest first), the global per-name ceiling, and the daily purge.
        builder.HasIndex(a => new { a.AttemptName, a.ClientIp, a.AttemptedAt });
        builder.HasIndex(a => new { a.AttemptName, a.AttemptedAt });
        builder.HasIndex(a => a.AttemptedAt);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Outcome);

        builder.HasOne<StaffUser>().WithMany().HasForeignKey(a => a.UserId)
            .HasConstraintName("FK_LGIN_ATMP_USR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoginOutcomeRow>().WithMany().HasForeignKey(a => a.Outcome)
            .HasConstraintName("FK_LGIN_ATMP_OTCM_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}
