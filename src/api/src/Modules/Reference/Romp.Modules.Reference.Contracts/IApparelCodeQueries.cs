namespace Romp.Modules.Reference.Contracts;

/// <summary>A lookup row as other modules may see it: its stable code and display name.</summary>
public sealed record ApparelCode(short Id, string Code, string Name);

/// <summary>
/// Sprint 5 (labelling): the colour and size codes that make up a SKU and the names printed on a tag. The only way another
/// module reads these lookups (ADR 0002). One query per call however many ids are asked for; unknown ids are absent.
/// </summary>
public interface IApparelCodeQueries
{
    Task<IReadOnlyDictionary<short, ApparelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, ApparelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);
}
