using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>IDNT</c> schema (roles and permissions now; staff accounts, sessions, MFA and the audit trail as their
/// features land), including the module's outbox and inbox (ADR 0004).
/// </summary>
public sealed class IdntDbContext(DbContextOptions<IdntDbContext> options) : DbContext(options), IIdentityDbContext
{
    public DbSet<Role> Roles => Set<Role>();

    public DbSet<PermissionLookup> Permissions => Set<PermissionLookup>();

    public DbSet<RoleGrant> RolePermissions => Set<RoleGrant>();

    public DbSet<StaffUser> Staff => Set<StaffUser>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<PendingSignIn> PendingSignIns => Set<PendingSignIn>();

    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    async Task IIdentityDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    /// <summary>Arbitrary constant naming the "Owner set" lock; advisory locks are per database, so it only has to be unique among Romp's.</summary>
    private const long OwnerLockKey = 0x524F4D50_4F574E52; // "ROMPOWNR"

    async Task IIdentityDbContext.LockOwnersAsync(CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The Owner lock is held until the transaction ends, so it needs one.");
        }

        await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({OwnerLockKey})", cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("IDNT");
        modelBuilder.HasOutboxTable("IDNT");
        modelBuilder.HasInboxTable("IDNT");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdntDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
