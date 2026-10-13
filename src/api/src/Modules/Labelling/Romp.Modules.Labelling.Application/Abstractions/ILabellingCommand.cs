namespace Romp.Modules.Labelling.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Labelling so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface ILabellingCommand;
