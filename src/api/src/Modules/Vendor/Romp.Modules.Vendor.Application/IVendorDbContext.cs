using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

/// <summary>
/// The VNDR module's persistence abstraction (Dependency Inversion: Application owns this
/// interface, Infrastructure's <c>VendorDbContext</c> implements it) - lets query/command
/// handlers live in Application without depending on the Infrastructure project (ADR 0002).
/// </summary>
public interface IVendorDbContext
{
    // Domain.Vendor (not bare "Vendor"): the compiler otherwise resolves "Vendor" to the
    // Romp.Modules.Vendor namespace segment itself, not the entity type within it.
    DbSet<Domain.Vendor> Vendors { get; }

    DbSet<PurchaseOrder> PurchaseOrders { get; }

    /// <summary>A handler calls this itself only when it needs a DB-generated value back in its response before returning.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
