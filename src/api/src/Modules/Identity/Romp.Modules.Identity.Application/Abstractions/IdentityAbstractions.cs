using Microsoft.EntityFrameworkCore;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Identity so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IIdentityCommand;

/// <summary>Identity's persistence port (Application owns it, Infrastructure's IdntDbContext implements it; ADR 0002). Feature sets are added with their features.</summary>
public interface IIdentityDbContext
{
    DbSet<StaffUser> Staff { get; }

    DbSet<Role> Roles { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
