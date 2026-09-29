namespace Romp.Modules.Reference.Application.Abstractions;

/// <summary>
/// Marks a MediatR command as belonging to the REF module, so
/// Romp.Modules.Reference.Infrastructure's transaction behaviour (the only place allowed to know
/// about <c>ReferenceDbContext</c>) applies to it and nothing else. Queries don't implement this -
/// there's nothing to save.
/// </summary>
public interface IReferenceCommand;
