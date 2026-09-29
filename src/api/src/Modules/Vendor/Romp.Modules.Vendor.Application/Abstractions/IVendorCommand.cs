using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Abstractions;

/// <summary>
/// Marks a MediatR command as belonging to the VNDR module, so
/// Romp.Modules.Vendor.Infrastructure's transaction behaviour (the only place allowed to know
/// about <c>VendorDbContext</c>) applies to it and nothing else. Queries don't implement this -
/// there's nothing to save.
/// </summary>
public interface IVendorCommand;
