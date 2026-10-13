using System.Globalization;
using System.Text;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Domain.Tags;

/// <summary>
/// A one-page PDF preview of the tag (TAG-8), written by hand: a tag needs only rectangles and two built-in fonts, so no PDF
/// library is added (supply-chain risk, ADR 0010). Text is drawn as text and never interpreted; every string is reduced to
/// printable ASCII and escaped for PDF string syntax. The barcode is drawn from <see cref="Ean13"/>, the same pattern the tests
/// verify. The page is 50 mm × 30 mm (141.73 × 85.04 pt).
/// </summary>
public static class TagPdf
{
    private const double PageWidth = 141.73;
    private const double PageHeight = 85.04;
    private const double ModuleWidth = 0.9;

    public static byte[] Render(TagData tag)
    {
        var gtin = Gtin.Parse(tag.Gtin);
        var pattern = Ean13.Encode(gtin.Value);

        var content = new StringBuilder();
        content.Append("0 g\n");
        Text(content, "F2", 10, 8, 71, TagData.Brand);
        Text(content, "F1", 7, 8, 62, tag.PrintedName);
        Text(content, "F1", 6, 8, 54, tag.PrintedVariantLine);

        const double barLeft = 12;
        const double barBottom = 24;
        const double barHeight = 26;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != '1')
            {
                continue;
            }

            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == '1')
            {
                run++;
            }

            content.Append(Num(barLeft + (i * ModuleWidth))).Append(' ').Append(Num(barBottom)).Append(' ')
                .Append(Num(run * ModuleWidth)).Append(' ').Append(Num(barHeight)).Append(" re f\n");
            i += run - 1;
        }

        Text(content, "F1", 6, barLeft, 16.5, $"{gtin.Value[0]} {gtin.Value[1..7]} {gtin.Value[7..]}");
        Text(content, "F2", 11, 8, 6, tag.PrintedPrice);
        Text(content, "F1", 4.5, 70, 6, TagData.Origin);
        if (tag.IsConcession)
        {
            content.Append("0.6 w 100 4 26 14 re S\n");
            Text(content, "F2", 9, 105, 8, TagData.ConcessionMark);
        }

        return Assemble(content.ToString());
    }

    private static void Text(StringBuilder content, string font, double size, double x, double y, string text) =>
        content.Append("BT /").Append(font).Append(' ').Append(Num(size)).Append(" Tf ")
            .Append(Num(x)).Append(' ').Append(Num(y)).Append(" Td (").Append(PdfString(text)).Append(") Tj ET\n");

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Printable ASCII only, with the three PDF string metacharacters escaped; anything else becomes '?'.</summary>
    internal static string PdfString(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is < ' ' or > '~')
            {
                builder.Append('?');
            }
            else if (c is '\\' or '(' or ')')
            {
                builder.Append('\\').Append(c);
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static byte[] Assemble(string content)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append('\n').Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
