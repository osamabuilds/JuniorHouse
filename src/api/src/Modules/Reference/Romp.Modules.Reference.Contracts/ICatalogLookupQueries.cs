namespace Romp.Modules.Reference.Contracts;

/// <summary>A care instruction with the key of its ISO 3758 symbol (Sprint 7, owner decision D6).</summary>
public sealed record CareInstructionCode(short Id, string Code, string Name, string SymbolCode);

/// <summary>
/// Sprint 7 (Catalog): the lookups a product is validated against and its path is built from. The only way Catalog reads them
/// (ADR 0002). One query per call however many ids are asked for; unknown ids are absent. Retired rows are still returned so
/// history stays readable; callers that accept a new selection check the row's existence only (the admin pick-lists hide retired rows).
/// </summary>
public interface ICatalogLookupQueries
{
    Task<IReadOnlyDictionary<short, ApparelCode>> GetGendersAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, ApparelCode>> GetCategoriesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, ApparelCode>> GetAgeBracketsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, ApparelCode>> GetCollectionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, ApparelCode>> GetFibresAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, CareInstructionCode>> GetCareInstructionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    /// <summary>Size codes and names (size charts and variant tables).</summary>
    Task<IReadOnlyDictionary<short, ApparelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    /// <summary>Gender ids by lower-case code (a path segment); unknown segments are absent. Used to resolve a public path.</summary>
    Task<IReadOnlyDictionary<string, short>> GetGenderIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken);

    /// <summary>Category ids by lower-case code (a path segment); unknown segments are absent.</summary>
    Task<IReadOnlyDictionary<string, short>> GetCategoryIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken);

    /// <summary>Colour codes and names (variant tables).</summary>
    Task<IReadOnlyDictionary<short, ApparelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);
}
