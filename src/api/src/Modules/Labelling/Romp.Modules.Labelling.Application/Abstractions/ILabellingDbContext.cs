using Microsoft.EntityFrameworkCore;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;
using Romp.Modules.Labelling.Domain.PrintJobs;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Application.Abstractions;

/// <summary>Labelling's persistence port (Application owns it, Infrastructure's SkuDbContext implements it; ADR 0002).</summary>
public interface ILabellingDbContext
{
    DbSet<Variant> Variants { get; }

    DbSet<VariantGtin> Gtins { get; }

    DbSet<LabelWorkItem> WorkItems { get; }

    DbSet<PrintJob> PrintJobs { get; }

    DbSet<PrintAttempt> PrintAttempts { get; }

    DbSet<PrinterLookup> Printers { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
