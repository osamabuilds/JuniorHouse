using Microsoft.EntityFrameworkCore;
using Romp.Modules.Shipping.Domain.Bookings;
using Romp.Modules.Shipping.Domain.Manifests;

namespace Romp.Modules.Shipping.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Shipping so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IShippingCommand;

/// <summary>Shipping's persistence port (Application owns it, Infrastructure's ShpgDbContext implements it; ADR 0002).</summary>
public interface IShippingDbContext
{
    DbSet<Shipment> Shipments { get; }

    DbSet<ShipmentEvent> ShipmentEvents { get; }

    DbSet<Manifest> Manifests { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Takes a shipment for booking: one conditional statement that succeeds only while it is pending and due, and pushes its next-attempt time to
    /// <paramref name="until"/>. Two workers asking at once get one yes (BKG-5).
    /// </summary>
    Task<bool> ClaimForBookingAsync(long shipmentId, DateTimeOffset now, DateTimeOffset until, CancellationToken cancellationToken);
}

/// <summary>The business numbers of shipping, as settings and not code (ADR 0031). Bound from <c>Shipping</c> and validated when the host starts.</summary>
public sealed class ShippingOptions
{
    /// <summary>How long to wait before trying a failed booking again (Q-SHP-2).</summary>
    public int RetryMinutes { get; set; } = 5;

    /// <summary>How many automatic tries a booking gets before it waits for staff (Q-SHP-2).</summary>
    public int MaxAttempts { get; set; } = 12;

    /// <summary>How often shipments of a courier without webhooks are asked for their status (Q-SHP-4).</summary>
    public int PollMinutes { get; set; } = 30;

    /// <summary>The business hour (Pakistan time) when the end-of-day manifests are made (Q-MNF-1).</summary>
    public int ManifestHour { get; set; } = 17;

    /// <summary>How long one courier call may take before it counts as unreachable (CPT-2).</summary>
    public int CourierTimeoutSeconds { get; set; } = 10;

    /// <summary>How long a booking in progress is left alone before another worker may take it.</summary>
    public int ClaimMinutes { get; set; } = 2;

    /// <summary>Failed public tracking look-ups from one address before a CAPTCHA is asked for (SEC-06).</summary>
    public int TrackingFailuresBeforeCaptcha { get; set; } = 3;

    /// <summary>Per courier code (PRIMARY, SECONDARY): enabled, webhook secret, and whether it pushes status.</summary>
    public Dictionary<string, CourierSettings> Couriers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CourierSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>The key the courier signs its status pushes with. From configuration or secrets, never committed.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>False for a courier without webhooks: its shipments are polled instead (TRK-6).</summary>
    public bool PushesStatus { get; set; } = true;
}
