using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Application;

/// <summary>
/// The REF module's persistence abstraction (Dependency Inversion: Application owns this
/// interface, Infrastructure's <c>ReferenceDbContext</c> implements it) - lets the generic lookup
/// query/command handlers live in Application without Application depending on the Infrastructure
/// project, which <c>Romp.ArchitectureTests</c> forbids (ADR 0002). Handlers only query/stage
/// changes here; <c>ReferenceTransactionBehavior</c> (Infrastructure) is the only place that calls
/// SaveChanges. One generic accessor is enough because every command/query in this module is
/// itself generic over the lookup type (task SCRUM-172).
/// </summary>
public interface IReferenceDbContext
{
    DbSet<TLookup> Lookups<TLookup>() where TLookup : Lookup;

    /// <summary>
    /// A handler calls this itself only when it needs a DB-generated value (the lookup's
    /// identity <c>Id</c>) back in its response before returning - see <c>CreateLookupCommandHandler</c>.
    /// Every other handler leaves saving to <c>ReferenceTransactionBehavior</c>, which always calls
    /// this again afterwards regardless (a harmless no-op once nothing is pending).
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
