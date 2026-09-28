using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain;

namespace Romp.Modules.Catalog.Application;

/// <summary>
/// The CTLG module's persistence abstraction (Dependency Inversion: Application owns this
/// interface, Infrastructure's <c>CatalogDbContext</c> implements it) - lets query/command
/// handlers live in Application without depending on the Infrastructure project (ADR 0002).
/// </summary>
public interface ICatalogDbContext
{
    DbSet<Style> Styles { get; }

    /// <summary>A handler calls this itself only when it needs the DB-generated Style.Id back in its response (see CreateStyleCommandHandler).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
