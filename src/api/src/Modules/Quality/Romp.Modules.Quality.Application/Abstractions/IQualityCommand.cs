namespace Romp.Modules.Quality.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to QC so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IQualityCommand;
