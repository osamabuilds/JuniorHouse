namespace Romp.Modules.Storefront.Domain;

/// <summary>A lookup row as the storefront needs it (Reference's code, display name and order). Shared by navigation and listings.</summary>
public sealed record LookupRow(string Code, string Name, short SortSeq, bool IsActive);
