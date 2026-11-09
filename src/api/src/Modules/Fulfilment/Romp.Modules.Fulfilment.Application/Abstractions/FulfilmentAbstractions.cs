using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Fulfilment.Domain.PackStation;
using Romp.Modules.Fulfilment.Domain.PickExceptions;

namespace Romp.Modules.Fulfilment.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Fulfilment so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IFulfilmentCommand;

/// <summary>Fulfilment's persistence port (Application owns it, Infrastructure's FulfDbContext implements it; ADR 0002).</summary>
public interface IFulfilmentDbContext
{
    DbSet<FulfilmentOrder> Orders { get; }

    DbSet<FulfilmentLine> Lines { get; }

    DbSet<PickExceptionRecord> Exceptions { get; }

    DbSet<ScanEntry> Scans { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The business numbers of fulfilment, as settings and not code (ADR 0030 point 6, Q-FUL-1 to Q-FUL-3). Bound from <c>Fulfilment</c>
/// and validated when the host starts. The weights are guesses until real parcels have been weighed: the check is a safety net.
/// </summary>
public sealed class FulfilmentOptions
{
    /// <summary>A parcel goes in a poly bag only when its expected weight is at most this (grams).</summary>
    public int PolyBagMaxGrams { get; set; } = 500;

    /// <summary>A parcel goes in a poly bag only when it holds at most this many units.</summary>
    public int PolyBagMaxItems { get; set; } = 1;

    /// <summary>A weighed parcel may differ from the expected weight by the larger of this percentage and <see cref="WeightToleranceGrams"/>.</summary>
    public int WeightTolerancePercent { get; set; } = 15;

    public int WeightToleranceGrams { get; set; } = 60;

    /// <summary>What one garment weighs (grams) until per-category weights exist.</summary>
    public int GarmentGrams { get; set; } = 250;

    /// <summary>What the bag or box weighs (grams).</summary>
    public int PackagingGrams { get; set; } = 80;

    /// <summary>How long the compact scan log is kept (days).</summary>
    public int ScanLogRetentionDays { get; set; } = 90;
}
