using System.Globalization;

namespace Romp.Modules.Labelling.Domain.Tags;

/// <summary>
/// Everything printed on a price/barcode tag (§5.12): product name, colour and size, the GTIN barcode, the price, and a "2ND"
/// mark for concession stock (D6). Both the PDF preview and the ZPL are built from this one value (TAG-8).
/// </summary>
public sealed record TagData(
    string Sku,
    string Gtin,
    string ProductName,
    string ColourName,
    string SizeName,
    decimal Price,
    bool IsConcession)
{
    public const string Brand = "ROMP";
    public const string Origin = "Made in Pakistan";
    public const string ConcessionMark = "2ND";
    public const int MaxNameLength = 28;
    public const int MaxVariantLineLength = 28;

    /// <summary>Product name as it will print: cleaned and shortened (TAG-7).</summary>
    public string PrintedName => TextSanitizer.Clean(ProductName, MaxNameLength);

    /// <summary>"Red / 2-3 Years" as it will print.</summary>
    public string PrintedVariantLine => TextSanitizer.Clean($"{ColourName} / {SizeName}", MaxVariantLineLength);

    /// <summary>The price in PKR from a decimal, never a float (TAG-3): "Rs. 1,299" or "Rs. 1,299.50".</summary>
    public string PrintedPrice => "Rs. " + Price.ToString(Price % 1 == 0 ? "#,##0" : "#,##0.00", CultureInfo.InvariantCulture);
}
