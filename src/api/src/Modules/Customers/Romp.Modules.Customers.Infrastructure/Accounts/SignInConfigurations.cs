using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

/// <summary>CUST.SESS_RVKD_RSN_LKP row. The key is the enum so the foreign key from CUST_SESS needs no conversion.</summary>
internal sealed class RevokeReasonRow
{
    public CustomerSessionRevokeReason Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>CUST.LGIN_OTCM_LKP row.</summary>
internal sealed class LoginOutcomeRow
{
    public LoginOutcome Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Seed rows of the two sign-in lookups, written into the <c>AddCustomerSignIn</c> migration. Ids are the enum values and are never reused.</summary>
internal static class SignInSeed
{
    public static IReadOnlyList<RevokeReasonRow> RevokeReasons { get; } =
    [
        Reason(CustomerSessionRevokeReason.SignOut, "SIGN_OUT", "Signed out"),
        Reason(CustomerSessionRevokeReason.SignOutEverywhere, "SIGN_OUT_ALL", "Signed out everywhere"),
        Reason(CustomerSessionRevokeReason.RefreshReuse, "REFRESH_REUSE", "Rotated refresh value presented again"),
        Reason(CustomerSessionRevokeReason.NewSignIn, "NEW_SIGN_IN", "Replaced by a new sign-in"),
        Reason(CustomerSessionRevokeReason.PasswordChanged, "PWD_CHNG", "Password changed or reset"),
        Reason(CustomerSessionRevokeReason.SessionLimit, "SESS_LIMIT", "Oldest session over the per-customer limit"),
    ];

    public static IReadOnlyList<LoginOutcomeRow> LoginOutcomes { get; } =
    [
        Outcome(LoginOutcome.Ok, "OK", "Password accepted"),
        Outcome(LoginOutcome.BadCredentials, "BAD_CRED", "Unknown email or wrong password"),
        Outcome(LoginOutcome.Throttled, "THROTTLED", "Refused by the throttle"),
        Outcome(LoginOutcome.CaptchaFailed, "CAPTCHA", "CAPTCHA missing or failed"),
        Outcome(LoginOutcome.RefreshReuse, "REFRESH_REUSE", "Rotated refresh value presented again"),
    ];

    private static RevokeReasonRow Reason(CustomerSessionRevokeReason id, string code, string name) =>
        new() { Id = id, Code = code, Name = name, SortSeq = (short)id, IsActive = true };

    private static LoginOutcomeRow Outcome(LoginOutcome id, string code, string name) =>
        new() { Id = id, Code = code, Name = name, SortSeq = (short)id, IsActive = true };
}

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
        builder.HasData(SignInSeed.RevokeReasons);
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
        builder.HasData(SignInSeed.LoginOutcomes);
    }
}

internal sealed class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> builder)
    {
        builder.ToTable("CUST_SESS");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.CustomerId).HasColumnName("CUST_ID").IsRequired();
        builder.Property(s => s.AccessTokenHash).HasColumnName("ACSS_TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(s => s.RefreshTokenHash).HasColumnName("RFSH_TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(s => s.PreviousRefreshTokenHash).HasColumnName("PREV_RFSH_TOKN_HASH").HasMaxLength(32);
        builder.Property(s => s.KeepSignedIn).HasColumnName("KEEP_IND").IsRequired();
        builder.Property(s => s.StartedAt).HasColumnName("STRT_DTE").IsRequired();
        builder.Property(s => s.AccessExpiresAt).HasColumnName("ACSS_EXPY_DTE").IsRequired();
        builder.Property(s => s.RefreshExpiresAt).HasColumnName("RFSH_EXPY_DTE").IsRequired();
        builder.Property(s => s.AbsoluteExpiresAt).HasColumnName("ABS_EXPY_DTE").IsRequired();
        builder.Property(s => s.RotatedAt).HasColumnName("ROTD_DTE");
        builder.Property(s => s.RevokedAt).HasColumnName("RVKD_DTE");
        builder.Property(s => s.RevokeReason).HasColumnName("RVKD_RSN_ID").HasConversion<short?>();
        builder.Property(s => s.ClientIp).HasColumnName("CLNT_IP_ADDR").HasMaxLength(CustomerSession.ClientIpMaxLength);
        builder.Property(s => s.UserAgent).HasColumnName("USR_AGNT").HasMaxLength(CustomerSession.UserAgentMaxLength);
        builder.Ignore(s => s.DomainEvents);

        // Lookup by hash must find exactly one session.
        builder.HasIndex(s => s.AccessTokenHash).IsUnique();
        builder.HasIndex(s => s.RefreshTokenHash).IsUnique();
        builder.HasIndex(s => s.PreviousRefreshTokenHash).IsUnique();
        builder.HasIndex(s => s.CustomerId);
        builder.HasIndex(s => s.RevokeReason);
        builder.HasIndex(s => s.AbsoluteExpiresAt); // the daily purge

        builder.HasOne<Customer>().WithMany().HasForeignKey(s => s.CustomerId)
            .HasConstraintName("FK_CUST_SESS_CUST_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RevokeReasonRow>().WithMany().HasForeignKey(s => s.RevokeReason)
            .HasConstraintName("FK_CUST_SESS_RVKD_RSN_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("CUST_PWD_RSET");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.CustomerId).HasColumnName("CUST_ID").IsRequired();
        builder.Property(t => t.TokenHash).HasColumnName("TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(t => t.IssuedAt).HasColumnName("ISSD_DTE").IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("EXPY_DTE").IsRequired();
        builder.Property(t => t.UsedAt).HasColumnName("USED_DTE");
        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.CustomerId);
        builder.HasIndex(t => t.ExpiresAt); // the daily purge

        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId)
            .HasConstraintName("FK_CUST_PWD_RSET_CUST_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}

internal sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("CUST_EML_VRFY");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.CustomerId).HasColumnName("CUST_ID").IsRequired();
        builder.Property(t => t.Email).HasColumnName("EML").HasMaxLength(Customer.EmailMaxLength).IsRequired();
        builder.Property(t => t.TokenHash).HasColumnName("TOKN_HASH").HasMaxLength(32).IsRequired();
        builder.Property(t => t.IssuedAt).HasColumnName("ISSD_DTE").IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("EXPY_DTE").IsRequired();
        builder.Property(t => t.UsedAt).HasColumnName("USED_DTE");
        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.CustomerId);
        builder.HasIndex(t => t.ExpiresAt); // the daily purge

        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId)
            .HasConstraintName("FK_CUST_EML_VRFY_CUST_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}

internal sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("CUST_LGIN_ATMP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.AttemptedEmail).HasColumnName("ATMP_EML").HasMaxLength(LoginAttempt.EmailMaxLength).IsRequired();
        builder.Property(a => a.ClientIp).HasColumnName("CLNT_IP_ADDR").HasMaxLength(CustomerSession.ClientIpMaxLength).IsRequired();
        builder.Property(a => a.Outcome).HasColumnName("OTCM_ID").HasConversion<short>().IsRequired();
        builder.Property(a => a.AttemptedAt).HasColumnName("ATMP_DTE").IsRequired();
        builder.Property(a => a.CustomerId).HasColumnName("CUST_ID");

        // The throttle query (email + address, newest first), the per-email ceiling, and the daily purge.
        builder.HasIndex(a => new { a.AttemptedEmail, a.ClientIp, a.AttemptedAt });
        builder.HasIndex(a => new { a.AttemptedEmail, a.AttemptedAt });
        builder.HasIndex(a => a.AttemptedAt);
        builder.HasIndex(a => a.CustomerId);
        builder.HasIndex(a => a.Outcome);

        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId)
            .HasConstraintName("FK_CUST_LGIN_ATMP_CUST_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoginOutcomeRow>().WithMany().HasForeignKey(a => a.Outcome)
            .HasConstraintName("FK_CUST_LGIN_ATMP_OTCM_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasAuditColumns();
    }
}
