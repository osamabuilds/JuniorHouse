namespace Romp.Modules.Storefront.Domain;

/// <summary>The URL segment for a lookup code: lower case, <c>_</c> as <c>-</c> (the same rule as a product path).</summary>
public static class CodeSegment
{
    public static string Of(string lookupCode) => lookupCode.Trim().ToLowerInvariant().Replace('_', '-');
}
