using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.Roles;
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

    async Task IIdentityDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("IDNT");
        modelBuilder.HasOutboxTable("IDNT");
        modelBuilder.HasInboxTable("IDNT");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdntDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
