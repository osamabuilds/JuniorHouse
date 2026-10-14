using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.Receiving;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Application.Abstractions;

/// <summary>QC's persistence port (Application owns it, Infrastructure's QcDbContext implements it; ADR 0002).</summary>
public interface IQualityDbContext
{
    DbSet<InboundDelivery> Deliveries { get; }

    DbSet<GoodsReceipt> GoodsReceipts { get; }

    DbSet<QcBatch> Batches { get; }

    DbSet<QcSubBatch> SubBatches { get; }

    DbSet<Inspection> Inspections { get; }

    DbSet<SafetyCheck> SafetyChecks { get; }

    DbSet<ReworkReturn> ReworkReturns { get; }

    DbSet<AqlSampleRow> AqlSamples { get; }

    DbSet<AqlLimitRow> AqlLimits { get; }

    DbSet<GrnNumberSequence> GrnSequences { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
