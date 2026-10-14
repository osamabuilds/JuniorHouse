namespace Romp.Modules.Production.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to PROD so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IProductionCommand;
