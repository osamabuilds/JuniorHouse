namespace Romp.Modules.Catalog.Application.Abstractions;

/// <summary>
/// Marks a MediatR command as belonging to the CTLG module, so
/// Romp.Modules.Catalog.Infrastructure's transaction behaviour (the only place allowed to know
/// about <c>CatalogDbContext</c>) applies to it and nothing else. Queries don't implement this -
/// there's nothing to save.
/// </summary>
public interface ICatalogCommand;
