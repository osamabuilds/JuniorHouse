using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Identity so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IIdentityCommand;

/// <summary>Identity's persistence port (Application owns it, Infrastructure's IdntDbContext implements it; ADR 0002). Feature sets are added with their features.</summary>
public interface IIdentityDbContext
{
    DbSet<StaffUser> Staff { get; }

    DbSet<Role> Roles { get; }

    DbSet<Session> Sessions { get; }

    DbSet<LoginAttempt> LoginAttempts { get; }

    DbSet<PendingSignIn> PendingSignIns { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Stops tracking an entity so its unsaved changes are dropped, for example after losing an optimistic-concurrency race.</summary>
    void Discard(object entity);

    /// <summary>
    /// Serialises every change that could remove an active Owner (AC-9): blocks until no other transaction holds the Owner lock, then
    /// holds it until this transaction ends. Call it before reading who the Owners are, inside the command's transaction.
    /// </summary>
    Task LockOwnersAsync(CancellationToken cancellationToken);
}
