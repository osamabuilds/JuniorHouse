using Microsoft.EntityFrameworkCore;
using Romp.Modules.Returns.Domain.ReverseQc;

namespace Romp.Modules.Returns.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Returns so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IReturnsCommand;

/// <summary>Returns persistence port (Application owns it, Infrastructure's RtrnDbContext implements it; ADR 0002).</summary>
public interface IReturnsDbContext
{
    DbSet<ReturnCase> Cases { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
