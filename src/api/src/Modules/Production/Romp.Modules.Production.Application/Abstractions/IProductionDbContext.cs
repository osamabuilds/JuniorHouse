using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Application.Abstractions;

/// <summary>PROD's persistence port (Application owns it, Infrastructure's ProductionDbContext implements it; ADR 0002).</summary>
public interface IProductionDbContext
{
    DbSet<ProductionRun> Runs { get; }

    DbSet<Milestone> Milestones { get; }

    DbSet<PpSampleRound> PpRounds { get; }

    DbSet<PpSampleFile> PpFiles { get; }

    DbSet<DeliveryNote> DeliveryNotes { get; }

    DbSet<DeliveryNoteLine> DeliveryNoteLines { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
